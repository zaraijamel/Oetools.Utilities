#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (SimpleHttpProxyServer.cs) is part of Oetools.Utilities.Test.
// 
// Oetools.Utilities.Test is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.Utilities.Test is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities.Test. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion

using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Oetools.Utilities.Test.Lib.Http;

namespace Oetools.Utilities.Test.Archive.HttpFileServer {

    internal class SimpleHttpProxyServer {
        
        public int NbHandledRequests { get; private set; }
        
        private const int BufferSize = 1024 * 8;
        
        private string _serverBasicToken;

        public SimpleHttpProxyServer(string user, string password) {
            _serverBasicToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}"));
        }

        public void OnHttpRequest(HttpListenerRequest request, HttpListenerResponse response) {
            try {
                // handle basic authent
                var receivedBasicToken = request.Headers.GetHeaderValues("Proxy-Authorization")?.FirstOrDefault();
                if (!string.IsNullOrEmpty(_serverBasicToken)) {
                    if (string.IsNullOrEmpty(receivedBasicToken)) {
                        response.WithHeader("Proxy-Authenticate", "Basic").WithCode(HttpStatusCode.ProxyAuthenticationRequired).AsText("Authentication required.");
                        return;
                    }
                    if (receivedBasicToken.Length <= 6 || !_serverBasicToken.Equals(receivedBasicToken.Substring(6))) {
                        response.WithCode(HttpStatusCode.Forbidden).AsText("Incorrect user/password.");
                        return;
                    }
                }

                var httpRequest = WebRequest.CreateHttp(request.RawUrl);
                httpRequest.Timeout = Timeout.Infinite;
                httpRequest.ReadWriteTimeout = Timeout.Infinite;
                httpRequest.Expect = null;
                httpRequest.ServicePoint.Expect100Continue = false;

                httpRequest.Method = request.HttpMethod;

                foreach (string header in request.Headers.AllKeys) {
                    httpRequest.Headers[header] = request.Headers[header];
                }
                
                //httpRequest.Proxy = new WebProxy("http://mylocalhost:8888") {
                //    UseDefaultCredentials = false,
                //    BypassProxyOnLocal = false
                //};
                
                // write to upstream
                if (!httpRequest.Method.Equals("get", StringComparison.OrdinalIgnoreCase) &&
                    !httpRequest.Method.Equals("head", StringComparison.OrdinalIgnoreCase) &&
                    !httpRequest.Method.Equals("connect", StringComparison.OrdinalIgnoreCase)) {
                    using (var upStream = httpRequest.GetRequestStream()) {
                        byte[] buffer = new byte[BufferSize];
                        int nbBytesRead;
                        while ((nbBytesRead = request.InputStream.Read(buffer, 0, buffer.Length)) > 0) {
                            upStream.Write(buffer, 0, nbBytesRead);
                            upStream.Flush();
                        }
                    }
                }

                // get response
                using (var httpWebResponse = (HttpWebResponse) httpRequest.GetResponse()) {
                    // read downstream
                    using (var downStream = httpWebResponse.GetResponseStream()) {
                        if (downStream != null) {
                            byte[] buffer = new byte[BufferSize];
                            int nbBytesRead;
                            while ((nbBytesRead = downStream.Read(buffer, 0, buffer.Length)) > 0) {
                                response.OutputStream.Write(buffer, 0, nbBytesRead);
                                response.OutputStream.Flush();
                            }
                        }
                    }
                    
                    response.WithCode(httpWebResponse.StatusCode);
                    foreach (var key in httpWebResponse.Headers.AllKeys) {
                        response.WithHeader(key, httpWebResponse.Headers[key]);
                    }
                }
                
            } catch (Exception e) {
                response.WithCode(HttpStatusCode.InternalServerError).AsText(e.Message);
            } finally {
                response.Close();
                NbHandledRequests++;
            }
        }
    }
}