﻿#region header

// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UtilsTest.cs) is part of Oetools.Utilities.Test.
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

using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Test.Tests {
    [TestClass]
    public class ProcessIoTest {
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(ProcessIoTest)));

        [TestMethod]
        public void ProcessIo_Exit0() {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return;
            }

            Directory.CreateDirectory(TestFolder);
            var process = new ProcessIo(@"C:\Windows\System32\net.exe");
            process.Execute("use");
            Assert.AreEqual(0, process.ExitCode);
            Assert.IsTrue(process.ErrorOutput.Length == 0);
            Assert.IsTrue(process.StandardOutput.Length > 0);
            Directory.Delete(TestFolder);
        }

        [TestMethod]
        public void ProcessIo_ExitNonZero() {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                return;
            }

            Directory.CreateDirectory(TestFolder);
            var process = new ProcessIo(@"C:\Windows\System32\net.exe");
            process.Execute("use 7874987498");
            Assert.AreNotEqual(0, process.ExitCode);
            Assert.IsTrue(process.ErrorOutput.Length > 0);
            Assert.IsTrue(process.StandardOutput.Length == 0);
            Directory.Delete(TestFolder);
        }
    }
}