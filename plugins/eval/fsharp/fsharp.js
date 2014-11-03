/*
 *  Copyright 2014 TWO SIGMA OPEN SOURCE, LLC
 *
 *  Licensed under the Apache License, Version 2.0 (the "License");
 *  you may not use this file except in compliance with the License.
 *  You may obtain a copy of the License at
 *
 *         http://www.apache.org/licenses/LICENSE-2.0
 *
 *  Unless required by applicable law or agreed to in writing, software
 *  distributed under the License is distributed on an "AS IS" BASIS,
 *  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *  See the License for the specific language governing permissions and
 *  limitations under the License.
 */

/**
 * FSharp eval plugin
 * For creating and config evaluators that evaluate FSharp code and update code cell results.
 */
define(function (require, exports, module, bkSessionManager)
{
    'use strict';
    var PLUGIN_NAME = "FSharp";
    var COMMAND = "fsharp/fsharpPlugin";
    var serviceBase = null;
    var subscriptions = {};

    var initialized = false;
    var FSharp = {
        pluginName: PLUGIN_NAME,
        cmMode: "text/x-fsharp",
        background: "#30B9DB",
        bgColor: "#378BBA",
        fgColor: "#FFFFFF",
        borderColor: "",
        shortName: "F#",

        newShell: function (shellId, cb)
        {
            if (!shellId)
            {
                shellId = "";
            }
            bkHelper.httpPost(serviceBase + "/fsharp/getShell", { shellId: shellId })
                .success(cb)
                .error(function ()
                {
                    console.log("failed to create shell", arguments);
                });
        },

        evaluate: function (code, modelOutput)
        {
            var deferred = Q.defer();
            var self = this;
            var progressObj = {
                type: "BeakerDisplay",
                innertype: "Progress",
                object: {
                    message: "submitting ...",
                    startTime: new Date().getTime()
                }
            };

            modelOutput.result = progressObj;

            $.ajax({
                type: "POST",
                datatype: "json",
                url: serviceBase + "/fsharp/evaluate",
                data: { shellId: self.settings.shellID, code: code }
            }).done(function (ret)
            {
                if (ret.status === 0)
                {
                    if (ret.result.ContentType.indexOf("image/") === 0)
                    {
                        modelOutput.result =
                        {
                            type: "BeakerDisplay",
                            innertype: 'Html',
                            object: '<img src="data:' + ret.result.ContentType + ';base64,' + ret.result.Data + '" />'
                        };
                    }
                    else if (ret.result.ContentType === "table/grid")
                    {
                        modelOutput.result =
                        {
                            type: "TableDisplay",
                            tableDisplayModel: {
                                columnNames: ret.result.Data.Columns,
                                values: ret.result.Data.Rows
                            },
                            columnNames: ret.result.Data.Columns,
                            values: ret.result.Data.Rows
                        };
                    }
                    else if (ret.result.ContentType === "text/html")
                    {
                        modelOutput.result =
                        {
                            type: "BeakerDisplay",
                            innertype: 'Html',
                            object: ret.result.Data
                        };
                    }
                    else
                    {
                        modelOutput.result = ret.result.Data;
                    }
                }
                else
                {
                    modelOutput.result =
                    {
                        type: "BeakerDisplay",
                        innertype: "Error",
                        object: ret.result.Data
                    };
                }
                modelOutput.elapsedTime = new Date().getTime() - progressObj.object.startTime;
                bkHelper.refreshRootScope();
                deferred.resolve();
            });

            return deferred.promise;
        },

        autocomplete: function (code, cpos, cb)
        {
            var self = this;
            $.ajax({
                type: "POST",
                datatype: "json",
                url: serviceBase + "/fsharp/autocomplete",
                data: { shellId: self.settings.shellID, code: code, caretPosition: cpos }
            }).done(function (x)
            {
                cb(x.Declarations);
            });
        },

        exit: function (cb)
        {
            var self = this;
            $.ajax({
                type: "POST",
                datatype: "json",
                url: serviceBase + "/fsharp/exit",
                data: { shellId: self.settings.shellID }
            }).done(cb);
        },

        updateShell: function (cb)
        {
            bkHelper.httpPost(serviceBase + "/fsharp/setShellOptions", {
                shellId: this.settings.shellID
            }).success(cb);
        },

        spec: {}
    };

    var shellReadyDeferred = bkHelper.newDeferred();
    var init = function ()
    {
        function loadedScripts()
        {
            bkHelper.locatePluginService(PLUGIN_NAME, {
                command: COMMAND,
                startedIndicator: "Successfully started server",
                waitfor: "Successfully started server",
                recordOutput: "true"
            }).success(function (ret)
            {
                serviceBase = ret;

                var FSharpShell = function (settings, doneCB)
                {
                    var self = this;
                    var setShellIdCB = function (id)
                    {
                        settings.shellID = id;
                        self.settings = settings;
                        function cb()
                        {
                            if (bkHelper.hasSessionId())
                            {
                                var initCode = "let beaker = new NamespaceClient(\"" + bkHelper.getSessionId() + "\")"
                                self.evaluate(initCode, {}).then(function ()
                                {
                                    if (doneCB)
                                    {
                                        doneCB(self);
                                    }
                                });
                            }
                            else
                            {
                                if (doneCB)
                                {
                                    doneCB(self);
                                }
                            }
                        }
                        self.updateShell(cb);
                    };

                    if (!settings.shellID)
                    {
                        settings.shellID = "";
                    }
                    this.newShell(settings.shellID, setShellIdCB);
                    this.perform = function (what)
                    {
                        var action = this.spec[what].action;
                        this[action]();
                    };
                };
                FSharpShell.prototype = FSharp;
                shellReadyDeferred.resolve(FSharpShell);
            }).error(function ()
            {
                console.log("failed to locate plugin service", PLUGIN_NAME, arguments);
            });
        }

        // load the syntax highlighter then load everything else
        bkHelper.loadList(["vendor/bower_components/codemirror/mode/mllike/mllike.js", "vendor/bower_components/codemirror/addon/comment/comment.js"], loadedScripts, loadedScripts);

    };
    init();

    exports.getEvaluatorFactory = function ()
    {
        return shellReadyDeferred.promise.then(function (Shell)
        {
            return {
                create: function (settings)
                {
                    var deferred = bkHelper.newDeferred();
                    new Shell(settings, function (shell)
                    {
                        deferred.resolve(shell);
                    });
                    return deferred.promise;
                }
            };
        });
    };

    exports.name = PLUGIN_NAME;
});