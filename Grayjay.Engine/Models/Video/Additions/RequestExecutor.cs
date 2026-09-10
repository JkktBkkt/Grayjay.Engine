using Grayjay.Engine.Exceptions;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using Microsoft.ClearScript.V8;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Models.Video.Additions
{
    public class RequestExecutor
    {
        private PluginConfig _config;
        private GrayjayPlugin _plugin;
        private IJavaScriptObject _executor;

        [V8Property("urlPrefix", true)]
        public string UrlPrefix { get; set; }

        public bool HasCleanup { get; private set; }

        public bool DidCleanup { get; private set; }

        public RequestExecutor(GrayjayPlugin plugin, IJavaScriptObject obj)
        {
            _executor = obj;
            _plugin = plugin;
            _config = plugin.Config;

            if (!obj.HasFunction("executeRequest"))
                throw new ScriptImplementationException(plugin.Config, "RequestExecutor is missing executeRequest");

            HasCleanup = obj.HasFunction("cleanup");
        }

        public byte[] ExecuteRequest(string url, Dictionary<string, string> headers)
        {
            if (_executor == null)
                throw new InvalidOperationException("Executor object is closed");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {

                var result = _executor.InvokeV8(_config, "executeRequest", url, MarshalHeaders(headers));
                return HandleExecuteRequestResult(result);
            }
            finally
            {
                stopwatch.Stop();
                Logger.Info<RequestExecutor>("RequestExecutor executeRequest finished in " + stopwatch.Elapsed.TotalMilliseconds + "ms");
            }
        }

        public byte[] ExecuteRequest(string url, Dictionary<string, string> headers, string method, byte[] body)
        {
            if (_executor == null)
                throw new InvalidOperationException("Executor object is closed");

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                object jsBody = null;
                if (body != null)
                {
                    var typedArrayObj = GetEngineOrThrow().Evaluate("new Uint8Array(" + body.Length.ToString() + ")");
                    if (!(typedArrayObj is ITypedArray typedBody))
                        throw new InvalidOperationException("Expected a Uint8Array, but got " + typedArrayObj?.GetType()?.ToString());
                    if (body.Length != 0)
                        typedBody.ArrayBuffer.WriteBytes(body, 0, (ulong)body.Length, 0);
                    jsBody = typedArrayObj;
                }

                var result = _executor.InvokeV8(_config, "executeRequest", url, MarshalHeaders(headers), method, jsBody);
                return HandleExecuteRequestResult(result);
            }
            finally
            {
                stopwatch.Stop();
                Logger.Info<RequestExecutor>("RequestExecutor executeRequest finished in " + stopwatch.Elapsed.TotalMilliseconds + "ms");
            }
        }

        //Headers must be a real JS object so plugins can pass them straight into http.* calls
        private object MarshalHeaders(Dictionary<string, string> headers)
        {
            object jsHeaders = headers;
            if (headers != null && GetEngineOrThrow().Evaluate("({})") is IScriptObject headerObject)
            {
                foreach (var pair in headers)
                    headerObject.SetProperty(pair.Key, pair.Value);
                jsHeaders = headerObject;
            }
            return jsHeaders;
        }

        private V8ScriptEngine GetEngineOrThrow()
        {
            return _plugin.GetUnderlyingEngine()
                ?? throw new InvalidOperationException("Executor object is closed");
        }

        private byte[] HandleExecuteRequestResult(object result)
        {
            //Host functions like utility.fromBase64 return .NET byte[] straight through V8
            if (result is byte[] rawBytes)
                return rawBytes;
            if (result is string str)
            {
                var base64Result = Convert.FromBase64String(str);
                return base64Result;
            }
            else if (result is ITypedArray typedArray)
            {
                var buffer = typedArray.ArrayBuffer;
                byte[] data = new byte[buffer.Size];
                buffer.ReadBytes(0, buffer.Size, data, 0);
                return data;
            }
            else if (result is IArrayBuffer buffer)
            {
                byte[] data = new byte[buffer.Size];
                buffer.ReadBytes(0, buffer.Size, data, 0);
                return data;
            }
            else
                throw new NotImplementedException();
        }

        public virtual void Cleanup()
        {
            DidCleanup = true;
            if (!HasCleanup || _executor == null)
                return;

            try
            {
                _executor.InvokeV8(_config, "cleanup");
            }
            catch(InvalidOperationException ex)
            {
                //Already cleaned up?
            }
            finally { }
        }

        ~RequestExecutor()
        {
            Cleanup();
        }
    }

}
