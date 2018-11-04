#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (HttpFileServerArchiverTest.cs) is part of Oetools.Utilities.Test.
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
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Archive;
using Oetools.Utilities.Archive.HttpFileServer;
using Oetools.Utilities.Lib.Http;
using Oetools.Utilities.Test.Lib.Http;

namespace Oetools.Utilities.Test.Archive.HttpFileServer {

    [TestClass]
    public class HttpFileServerArchiverTest : ArchiveTest {

        private static string _testFolder;
        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(HttpFileServerArchiverTest)));

        [ClassInitialize]
        public static void Init(TestContext context) {
            Cleanup();
            Directory.CreateDirectory(TestFolder);
        }

        [ClassCleanup]
        public static void Cleanup() {
            if (Directory.Exists(TestFolder)) {
                Directory.Delete(TestFolder, true);
            }
        }

        [TestMethod]
        public void Test() {
            // hostname to use
            // we need something different than 127.0.0.1 or localhost for the proxy!
            IPHostEntry hostEntry;
            try {
                hostEntry = Dns.GetHostEntry("mylocalhost");
            } catch (Exception) {
                hostEntry = null;
            }
            var host = hostEntry == null ? "127.0.0.1" : "mylocalhost";
            
            var archiver = Archiver.New(ArchiverType.HttpFileServer) as IHttpFileServerArchiver;
            Assert.IsNotNull(archiver);

            var baseDir = Path.Combine(TestFolder, "http");

            
            archiver.SetProxy($"http://{host}:8085/", "jucai69d", "julien caillon");
            archiver.SetBasicAuthentication("admin", "admin123");
            
            var listFiles = GetPackageTestFilesList(TestFolder, $"http://{host}:8084/server1");
            listFiles.AddRange(GetPackageTestFilesList(TestFolder, $"http://{host}:8084/server2"));
            
            var fileServer = new SimpleHttpFileServer(baseDir, "admin", "admin123");
            var proxyServer = new SimpleHttpProxyServer("jucai69d", "julien caillon");
            
            var cts = new CancellationTokenSource();
            var task1 = HttpServer.ListenAsync(8084, cts.Token, fileServer.OnHttpRequest, true);
            var task2 = HttpServer.ListenAsync(8085, cts.Token, proxyServer.OnHttpRequest, true);

//            var req = new HttpRequest($"http://{host}:8084/server1");
//            req.UseProxy($"http://{host}:8085/", "jucai69d", "julien caillon");
//            req.UseBasicAuthorizationHeader("admin", "admin123");
//            req.PutFile("derp", @"E:\Download\Vegan-Meal-Challenge-Recipe-Book.pdf");
//            Task.WaitAll(task1, task2);
            
            PartialTestForHttpFileServer(archiver, listFiles);
            
            HttpServer.Stop(cts, task1, task2);
        }
    }
}