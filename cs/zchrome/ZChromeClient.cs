using Microsoft.IdentityModel.Tokens;
using Microsoft.VisualBasic;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Configuration;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Policy;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Navigation;
using XChrome.cs.db;
using XChrome.cs.tools.socks5;
using XChrome.cs.win32;
using XChrome.cs.xchrome;
using XChrome.cs.zchrome;

namespace XChrome.cs.zchrome
{
    /// <summary>
    /// 指纹伪装的配置参数
    /// </summary>
    public class FingerprintConfig
    {
        public string UserAgent { get; set; }
        public string Local { get; set; }
        public bool Mobile { get; set; } = false;
        public string TimezoneId {  get; set; }
        public bool HasTouch { get; set; }
        public float Latitude { get; set; } = 0;
        public float Longitude { get; set; } = 0;
        public Dictionary<string, string>? ExtraHTTPHeaders { get; set; } = null;
        public string proxy { get; set; } = "";

        public int Width { get; set; } = 0;
        public int Height { get; set; } = 0;
        public double DeviceScaleFactor { get; set; } = 1.0;
    }

    public class ChromeDebugPortTools
    {
        private static int startPort = 9222;
        private static object _lock=new object();
        /// <summary>
        /// 线程安全的可用端口获取
        /// </summary>
        /// <returns></returns>
        public static async Task<(bool isSuccess,int port,string? errmsg)> GetChromeDebug_CanUse_Port()
        {
            int port = GetPort();
            bool isHave = await IsHaveChromeDevToolsAsync(port);
            while (isHave)
            {
                if (port >= 10000)
                {
                    break;
                }
                port++;
                isHave = await IsHaveChromeDevToolsAsync(port);
            }
            if (port >= 10000)
            {
                return (false,0, "尝试端口次数过多！");
            }


            return (true,port,"");
        }



        private static int GetPort()
        {
            int port = 0;
            lock (_lock)
            {
                port = startPort;
                startPort++;
                if (port >= 19222)
                {
                    startPort = 9222;
                    port = startPort;
                }
            }
            return port;
        }
        /// <summary>
        /// 使用 HttpClient 轮询调试接口，确定Chrome调试端口是否可用
        /// </summary>
        /// <param name="port">远程调试端口</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <param name="pollingInterval">轮询间隔（毫秒）</param>
        /// <returns>如果端口上可获取调试信息返回 true</returns>
        private static async Task<bool> IsHaveChromeDevToolsAsync(int port)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(1);
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var response = await httpClient.GetAsync($"http://localhost:{port}/json/version");
                    if (response.IsSuccessStatusCode)
                    {
                        string xx = await response.Content.ReadAsStringAsync();
                        string _webSocketDebuggerUrl = "";
                        using (JsonDocument doc = JsonDocument.Parse(xx))
                        {
                            _webSocketDebuggerUrl = doc.RootElement.GetProperty("webSocketDebuggerUrl").GetString();
                        }
                        // 一旦成功响应，就认为 Chrome 调试接口已启动
                        return true;
                    }
                }
                catch (Exception)
                {
                    // 请求失败（可能 Chrome 还没启动），等待下一次轮询
                    return false;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Chrome DevTools 协议客户端封装  
    /// </summary>
    public class ZChromeClient : IDisposable
    {
        private Process _chromeProcess;
        private ZWebSocket _wsClient;
        private string _webSocketDebuggerUrl;
        private int _commandId = 0;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingCommands = new();
        // 存储 targetId 与 sessionId 映射，方便后续对某个页面/target发命令
        private readonly ConcurrentDictionary<string, string> _sessionMap = new();
        private FingerprintConfig _fingerprintConfig;
        private string _injectJS;
        private XChromeClient _xchrome;
        private bool _isHomePageInjSuccess = false;

        /// <summary>
        /// 当检测到新页面创建时触发，对应 Target.targetCreated 事件
        /// </summary>
        public event Action<JsonElement> PageCreated;
        /// <summary>
        /// 当检测到页面导航时触发，对应 Page.frameNavigated 事件
        /// </summary>
        public event Action<JsonElement> PageNavigated;
        /// <summary>
        /// 当检测到页面关闭时触发，对应 Target.targetDestroyed 事件
        /// </summary>
        public event Action<JsonElement> PageClosed;
        /// <summary>
        /// 当检测到弹窗 (JS 对话框) 时触发，对应 Page.javascriptDialogOpening 事件
        /// </summary>
        public event Action<JsonElement> DialogOpened;
         
        public event Action<long> ChromeClose;

        /// <summary>
        /// 用于日志输出（也可直接重定向到 Console.WriteLine）
        /// </summary>
        public event Action<string> Log;
        public CancellationTokenSource CancellationTokenSource_proxy_server;

        private ZJob? zjob;

        public ZChromeClient(ZJob _zjob)
        {
            zjob = _zjob;
            CancellationTokenSource_proxy_server = new CancellationTokenSource();

        }


        


        /// <summary>
        /// 启动 Chrome，并连接远程调试端口  
        /// 参数说明：  
        /// chromePath：Chrome 可执行文件路径  
        /// remoteDebuggingPort：远程调试端口，默认 9222  
        /// userDataDir：用户数据目录，不指定时随机生成  
        /// extraArguments：额外的启动参数
        /// </summary>
        public async Task<(bool isSuccess,string errMsg)> LaunchChromeAsync(string chromePath, string userDataDir,XChromeClient xchrome, FingerprintConfig finger, string extraArguments = "")
        {
            _xchrome = xchrome; 
            //PageCreated += (p) =>
            //{

            //};
            //PageClosed += (p) =>
            //{

            //};

            await SetFingerprintAsync(finger);


            //第一步，先寻找可用端口
            var cport = await ChromeDebugPortTools.GetChromeDebug_CanUse_Port();
            if (cport.isSuccess == false)
            {
                return (false, cport.errmsg??"");
            }
            int port = cport.port;
            

            //第二步，打开浏览器
            //代理
            string proxy = "";
            if (finger.proxy != "")
            {
                var pp=ZChromeManager.Instance._ManagerTooler.getProxy2(finger.proxy);
                if (pp != null) {
                    int portt= await Proxy2ProxyPools.AddMapping_BackLocalPort(pp.Value.protocol, pp.Value.Address, pp.Value.Port, pp.Value.name, pp.Value.pass, CancellationTokenSource_proxy_server.Token);
                    Debug.WriteLine("端口===" + portt);
                    proxy = "--proxy-server=\"http=127.0.0.1:"+ portt + ";https=127.0.0.1:"+ portt + "\"";
                }
            }

            string postion = $"--window-position={xchrome.StartLeft},{xchrome.StartTop} --window-size={xchrome.StartWidth},{xchrome.StartHeight}";

            string args = $"--remote-debugging-port={port} --new-window --user-data-dir=\"{userDataDir}\" {postion} {proxy} {extraArguments}";
            var startInfo = new ProcessStartInfo(chromePath, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            _chromeProcess = Process.Start(startInfo);
            Log?.Invoke("Chrome launched.");


            // 检查 chrome 进程是否意外退出
            if (_chromeProcess==null || _chromeProcess.HasExited)
            {
                Log("Chrome process terminated unexpectedly.");
                return (false,"chrome进程意外退出");
            }


            // 使用轮询方式判断调试接口是否启动成功
            var isDebugInterfaceReady = await WaitForChromeDevToolsAsync(port, 10000, 500);
            if (!isDebugInterfaceReady.isSuccess)
            {
                Log("Chrome debug interface is available.");
                return (false,"chrome没有打开成功！");
            }
            if (zjob != null) { 
                zjob.AddProcess(_chromeProcess);
            }

            //等待窗体，获取句柄
            _chromeProcess.WaitForInputIdle();
            IntPtr hWnd = _chromeProcess.MainWindowHandle;
            int retries = 0;
            while (hWnd == IntPtr.Zero && retries++ < 50)
            {
                Thread.Sleep(100);  // 每隔 100 毫秒检查一次
                _chromeProcess.Refresh();
                hWnd = _chromeProcess.MainWindowHandle;
            }

            //绑定句柄
            xchrome.Hwnd = hWnd;
            xchrome.ProcessId = (uint)_chromeProcess.Id;

            _=Task.Run(async () =>
            {
                _webSocketDebuggerUrl = isDebugInterfaceReady.socketurl;
                //Log("zzzz33:"+sw.ElapsedMilliseconds+" 毫秒");
                Log?.Invoke($"Debugger URL: {_webSocketDebuggerUrl}");
                //sw.Restart();
                // 连接到调试 WebSocket
                _wsClient = new ZWebSocket();
                //sw.Restart() ;
                //Log("zzzz4.5：" + sw.ElapsedMilliseconds + " 毫秒");
                _wsClient.MessageReceived += OnMessageReceived;
                _wsClient.Error += (ex) => {
                    //关闭
                    ChromeClose?.Invoke(xchrome.Id);
                    CancellationTokenSource_proxy_server.Cancel();
                };
                _wsClient.Disconnected += () =>
                {
                    //关闭
                    ChromeClose?.Invoke(xchrome.Id);
                };

                await _wsClient.ConnectAsync(new Uri(_webSocketDebuggerUrl));

                //自动附加
                await SetAutoAttachAsync();


                //获得第一个target ,打开首页
                var res = await SendCommandAsync("Target.getTargets");
                if (!res.isSuccess)
                {
                    cs.Loger.Err(res.errmsg ?? "");
                    //return (false, res.errmsg ?? "");
                    return;
                }
                var jj = Newtonsoft.Json.JsonConvert.DeserializeObject(res.json.Value.ToString()) as JObject;
                var tid = jj["targetInfos"][0]["targetId"].ToString();
                //await Task.Delay(2000);
                int timeoutnum = 100;
                while (!_isHomePageInjSuccess)
                {
                    await Task.Delay(100);
                    timeoutnum--;
                    if (timeoutnum == 0) { break; }
                }
                await SendCommandAsync("Page.navigate", new { url = "https://www.web3tool.vip/browser?id=" + xchrome.Id + "&u=" + cs.Config.userid }, _sessionMap[tid]);


            });
            
            
            

            
            

            
            return (true, "");
        }



        /// <summary>
        /// 使用 HttpClient 轮询调试接口，确定Chrome调试端口是否可用
        /// </summary>
        /// <param name="port">远程调试端口</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <param name="pollingInterval">轮询间隔（毫秒）</param>
        /// <returns>如果端口上可获取调试信息返回 true</returns>
        private async Task<(bool isSuccess,string? socketurl)> WaitForChromeDevToolsAsync(int port, int timeoutMs, int pollingInterval)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.Timeout = TimeSpan.FromSeconds(1);
                var stopwatch = Stopwatch.StartNew();
                while (stopwatch.ElapsedMilliseconds < timeoutMs)
                {
                    try
                    {
                        var response = await httpClient.GetAsync($"http://localhost:{port}/json/version");
                        if (response.IsSuccessStatusCode)
                        {
                            string xx=await response.Content.ReadAsStringAsync();
                            string _webSocketDebuggerUrl = "";
                            using (JsonDocument doc = JsonDocument.Parse(xx))
                            {
                                _webSocketDebuggerUrl = doc.RootElement.GetProperty("webSocketDebuggerUrl").GetString();
                            }
                            // 一旦成功响应，就认为 Chrome 调试接口已启动
                            return (true, _webSocketDebuggerUrl);
                        }
                    }
                    catch (Exception)
                    {
                        // 请求失败（可能 Chrome 还没启动），等待下一次轮询
                    }
                    await Task.Delay(pollingInterval);
                }
                return (false,null);
            }
        }
        /// <summary>
        /// 开启自动 attach，新 target 创建时，Chrome 会自动附加到调试会话并下发 Target.attachedToTarget 事件
        /// </summary>
        private async Task SetAutoAttachAsync()
        {
            var parameters = new { autoAttach = true, waitForDebuggerOnStart = false, flatten = true };

            int id = ++_commandId;
            var messageDict = new Dictionary<string, object>
            {
                { "id", id },
                { "method", "Target.setAutoAttach" },
                 { "params", parameters }
            };

            

            string messageJson = System.Text.Json.JsonSerializer.Serialize(messageDict);
            var tcs = new TaskCompletionSource<JsonElement>();
            _pendingCommands.TryAdd(id, tcs);
            await _wsClient.SendMessageAsync(messageJson);

            Log?.Invoke("自动附加成功！");
        }

        /// <summary>
        /// 设置全局 JS 注入  
        /// 每次打开新页面时，都会自动执行该脚本
        /// </summary>
        /// <param name="script">JS 代码字符串</param>
        public void SetJsInjection(string script)
        {
            _injectJS = script;
        }

        /// <summary>
        /// 通过 DevTools 协议将 JS 注入到每个新页面中  
        /// 命令：Page.addScriptToEvaluateOnNewDocument
        /// </summary>
        private async Task AddScriptToEvaluateOnNewDocumentAsync(string script)
        {
            var parameters = new { source = script };
            await SendCommandAsync("Page.addScriptToEvaluateOnNewDocument", parameters);
            Log?.Invoke("Injected JS to be evaluated on new document.");
        }

        /// <summary>
        /// 设置浏览器指纹伪装参数，包括 user-agent、分辨率、语言等  
        /// 内部调用 Emulation 和 Network 命令，注意该命令会对当前已经连接的 browser target 生效，
        /// 新 attach 的 target 会在 Target.attachedToTarget 事件中自动应用。
        /// </summary>
        public async Task SetFingerprintAsync(FingerprintConfig config)
        {
            _fingerprintConfig = config;
        }

        private async Task SetUserAgentOverrideAsync(string userAgent,string targetId)
        {
            var parameters = new { userAgent };
            await SendCommandAsync("Emulation.setUserAgentOverride", parameters, sessionId: _sessionMap?[targetId]??null);
        }

        private async Task SetDeviceMetricsOverrideAsync(int width, int height, double deviceScaleFactor, bool mobile,string targetId)
        {
            // width, height,deviceScaleFactor
            var parameters = new { mobile };
            await SendCommandAsync("Emulation.setDeviceMetricsOverride", parameters, sessionId: _sessionMap?[targetId] ?? null);
        }

        /// <summary>
        /// 通过 Target.createTarget 命令打开新页面，参数 url 为页面地址  
        /// 返回创建后 target 的 targetId
        /// </summary>
        public async Task<(bool isSuccess, string? targetId,string? errmsg)> CreatePageAsync(string url)
        {
            var response = await SendCommandAsync("Target.createTarget", new { url });
            if (!response.isSuccess) {
                return (false, null, response.errmsg);
            }
            if(response.json.Value.TryGetProperty("targetId",out var tarId))
            {
                return (true, tarId.GetString(), null);
            }
            else
            {
                return (false, null, "返回json没有找到targetid:"+(response.json?.ToString()??""));
            }

        }

        public async Task<(bool isSuccess,string? x,string? errmsg)> NavigatePageAsync(string url)
        {
            var response = await SendCommandAsync("Page.navigate", new { url });
            if (!response.isSuccess)
            {
                return (false, null, response.errmsg);
            }
            if (response.json.Value.TryGetProperty("targetId", out var tarId))
            {
                return (true, tarId.GetString(), null);
            }
            else
            {
                return (false, null, "返回json没有找到targetid:" + (response.json?.ToString() ?? ""));
            }
        }

        /// <summary>
        /// 通过 Target.closeTarget 命令关闭页面  
        /// </summary>
        public async Task<(bool isSuccess, string? targetId, string? errmsg)> ClosePageAsync(string targetId)
        {
            var response = await SendCommandAsync("Target.closeTarget", new { targetId });
            if (!response.isSuccess)
            {
                return (false, null, response.errmsg);
            }
            if (response.json.Value.TryGetProperty("success", out var successElement) && successElement.GetBoolean())
            {
                return (true,targetId,null);
            }
            return (false,null,"返回："+ (response.json?.ToString() ?? ""));
        }

        /// <summary>
        /// 在指定 target 下执行 JavaScript 代码，使用 Runtime.evaluate 命令  
        /// targetId 对应的 sessionId 将在 Target.attachedToTarget 时保存到内部字典中
        /// </summary>
        public async Task<(bool isSuccess, JsonElement? json, string? errmsg)> ExecuteJsAsync(string targetId, string expression)
        {
            if (!_sessionMap.TryGetValue(targetId, out string sessionId))
            {
                Log?.Invoke($"No session attached for target: {targetId}");
                throw new Exception("Target not attached.");
            }
            var parameters = new { expression, awaitPromise = true };
            var response = await SendCommandAsync("Runtime.evaluate", parameters, sessionId);
            if (!response.isSuccess)
            {
                return (false, null, response.errmsg);
            }
            return (true, response.json, null);
        }

        /// <summary>
        /// 发送 DevTools 协议命令  
        /// 如果指定了 sessionId，则将命令发送到对应 target 的调试会话中
        /// </summary>
        public async Task<(bool isSuccess, JsonElement? json,string? errmsg)> SendCommandAsync(string method, object parameters = null, string sessionId = null,int timeout=5000)
        {
            int id = ++_commandId;
           
            // 构造消息对象。如果传入的 sessionId 为 null，也会被序列化为 null，不会影响命令解析。
            var messageDict = new Dictionary<string, object>
            {
                { "id", id },
                { "method", method },
                 { "params", parameters ?? new { } }
            };
            
            if (!string.IsNullOrEmpty(sessionId))
            {
                messageDict["sessionId"] = sessionId;
            }

            string messageJson = System.Text.Json.JsonSerializer.Serialize(messageDict);
            var tcs = new TaskCompletionSource<JsonElement>();
            _pendingCommands.TryAdd(id, tcs);
            //Debug.WriteLine("send===>" + messageJson); ;
            await _wsClient.SendMessageAsync(messageJson);


            // 同时启动一个超时任务
            var delayTask = Task.Delay(timeout);

            // 等待 TCS 的任务或者超时任务
            var completedTask = await Task.WhenAny(tcs.Task, delayTask);
            if (completedTask == tcs.Task)
            {
                try
                {
                    // 正常收到响应，将结果返回
                    return (true, await tcs.Task, null);
                }catch(Exception e)
                {
                    // 正常收到响应，将结果返回
                    return (false, null,e.Message);
                }
                
            }
            else
            {
                // 超时处理：移除待处理项，并抛出超时异常
                _pendingCommands.TryRemove(id, out _);
                return (false, null, $"等待命令 (id: {id}, method: {method}) 响应超时，第 {timeout} 毫秒秒内无响应。");
            }

        }

        /// <summary>
        /// 统一处理来自 Chrome 的 WebSocket 消息  
        /// 1. 如果消息包含 id，则为某个命令的响应，解析并通过任务完成回调返回结果。  
        /// 2. 如果消息包含 method，则为异步事件，根据 method 字段转发相应事件。
        /// </summary>
        private async void OnMessageReceived(string message)
        {
            //Debug.WriteLine("收到消息："+message);
            try
            {
                using (var doc = JsonDocument.Parse(message))
                {
                    var root = doc.RootElement;
                    if (root.TryGetProperty("id", out JsonElement idElement))
                    {
                        int id = idElement.GetInt32();
                        //有method id，需要放入回调

                        if (_pendingCommands.TryRemove(id, out var tcs))
                        {
                            if (root.TryGetProperty("result", out JsonElement result))
                            {
                                tcs.SetResult(result.Clone());
                            }
                            else if (root.TryGetProperty("error", out JsonElement error))
                            {
                                tcs.SetException(new Exception(error.ToString()));
                            }
                            else
                            {
                                tcs.SetResult(root.Clone());
                            }
                        }
                    }
                    else if (root.TryGetProperty("method", out JsonElement methodElement))
                    {
                        string methodName = methodElement.GetString();
                        switch (methodName)
                        {
                            case "Target.attachedToTarget":
                                {
                                    // 当一个新 target 被自动 attach 时，下发此事件。
                                    if (root.TryGetProperty("params", out JsonElement paramsElement))
                                    {
                                        string sessionId = paramsElement.GetProperty("sessionId").GetString();
                                        var targetInfo = paramsElement.GetProperty("targetInfo");
                                        string targetId = targetInfo.GetProperty("targetId").GetString();
                                        string type = targetInfo.GetProperty("type").GetString();
                                        _sessionMap[targetId] = sessionId;
                                        Log?.Invoke($"Attached to target: {targetId} with sessionId: {sessionId}");
                                        if (type == "page")
                                        {
                                            await SendCommandAsync("Page.enable", null, sessionId: sessionId);
                                        }
                                        


                                        // 针对新附加的 target，优先应用指纹配置（若已设置）
                                        if (_fingerprintConfig != null)
                                        {
                                            if (_fingerprintConfig.Local != "")
                                            {
                                                //await SendCommandAsync("Emulation.setLocaleOverride", parameters: new { locale = _fingerprintConfig.Local }, sessionId: sessionId);
                                                
                                            }
                                            if (_fingerprintConfig.TimezoneId != "")
                                            {
                                                await SendCommandAsync("Emulation.setTimezoneOverride", parameters: new { timezoneId = _fingerprintConfig.TimezoneId }, sessionId: sessionId);
                                            }
                                            if (_fingerprintConfig.HasTouch)
                                            {
                                                await SendCommandAsync("Emulation.setTouchEmulationEnabled", parameters: new { enabled = true}, sessionId: sessionId);
                                            }

                                            if (_fingerprintConfig.Latitude != 0)
                                            {
                                                await SendCommandAsync("Emulation.setGeolocationOverride", parameters: new { latitude = _fingerprintConfig.Latitude, longitude=_fingerprintConfig.Longitude, accuracy=1 }, sessionId: sessionId);
                                            }
                                            //js
                                            await SendCommandAsync("Page.addScriptToEvaluateOnNewDocument", parameters: new { 
                                                source = ZChromeManager.Instance._ManagerCache.GetInitScript(_fingerprintConfig.Local, _fingerprintConfig.HasTouch) 
                                            }, sessionId: sessionId);

                                            await SetUserAgentOverrideAsync(_fingerprintConfig.UserAgent, targetId);
                                            await SetDeviceMetricsOverrideAsync(_fingerprintConfig.Width, _fingerprintConfig.Height, _fingerprintConfig.DeviceScaleFactor, _fingerprintConfig.Mobile,targetId);
                                            
                                            if (_fingerprintConfig.ExtraHTTPHeaders!=null && _fingerprintConfig.ExtraHTTPHeaders.Count>0)
                                            {
                                                //var headers = new { AcceptLanguage = _fingerprintConfig.Language };
                                                await SendCommandAsync("Network.setExtraHTTPHeaders", _fingerprintConfig.ExtraHTTPHeaders, sessionId:sessionId);
                                            }

                                            _isHomePageInjSuccess = true;

                                           

                                        }
                                        // 如果设置了全局 JS 注入，则对当前 target 立即下发 Page.addScriptToEvaluateOnNewDocument 命令
                                       
                                    }
                                    break;
                                }
                            case "Target.targetCreated":
                                {
                                    if (root.TryGetProperty("params", out JsonElement p))
                                    {
                                        PageCreated?.Invoke(p);
                                    }
                                    break;
                                }
                            case "Target.targetDestroyed":
                                {
                                    if (root.TryGetProperty("params", out JsonElement p))
                                    {
                                        PageClosed?.Invoke(p);
                                    }
                                    break;
                                }
                            //case "Target.targetInfoChanged ":
                            //    {
                            //        if (root.TryGetProperty("params", out JsonElement p))
                            //        {
                            //            PageClosed?.Invoke(p);
                            //        }
                            //        break;
                            //    }

                            case "Page.frameNavigated":
                                {
                                    if (root.TryGetProperty("params", out JsonElement p))
                                    {
                                        PageNavigated?.Invoke(p);
                                    }
                                    break;
                                }
                            case "Page.javascriptDialogOpening":
                                {
                                    if (root.TryGetProperty("params", out JsonElement p))
                                    {
                                        DialogOpened?.Invoke(p);
                                    }
                                    break;
                                }
                            default:
                                {
                                    // 其它事件作为日志输出
                                    Log?.Invoke("Event [" + methodName + "] received: " + message);
                                    break;
                                }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log?.Invoke("Error processing message: " + ex.Message);
            }
        }

        public async Task<bool> CloseBrowserAsync()
        {
            var res = await SendCommandAsync("Browser.close");
            ChromeClose?.Invoke(_xchrome.Id);
            CancellationTokenSource_proxy_server.Cancel();
            return res.isSuccess;
        }

        public void Dispose()
        {
            CancellationTokenSource_proxy_server.Cancel();
            _wsClient?.Dispose();
            if (_chromeProcess != null && !_chromeProcess.HasExited)
            {
                try
                {
                    _chromeProcess.Kill();
                }
                catch { }
            }
            
        }
    }
}