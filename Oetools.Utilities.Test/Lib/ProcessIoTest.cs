﻿#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (ProcessIoTest.cs) is part of Oetools.Utilities.Test.
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib;

namespace Oetools.Utilities.Test.Lib {
    [TestClass]
    public class ProcessIoTest {
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(ProcessIoTest)));

        [TestMethod]
        public void ProcessIo_Exit0() {
            if (!Utils.IsRuntimeWindowsPlatform) {
                return;
            }

            var process = new ProcessIo(@"C:\Windows\System32\net.exe");
            process.Execute(new ProcessArgs().Append("use"));
            Assert.AreEqual(0, process.ExitCode);
            Assert.IsTrue(process.ErrorOutputArray.Count == 0);
            Assert.IsTrue(process.StandardOutputArray.Count > 0);
        }

        [TestMethod]
        public void ProcessIo_ExitNonZero() {
            if (!Utils.IsRuntimeWindowsPlatform) {
                return;
            }

            var process = new ProcessIo(@"C:\Windows\System32\net.exe");
            process.Execute(new ProcessArgs().Append("use").Append("7874987498"));
            Assert.AreNotEqual(0, process.ExitCode);
            Assert.IsTrue(process.ErrorOutputArray.Count > 0);
            Assert.IsTrue(process.StandardOutputArray.Count == 0);
        }

    }
}
