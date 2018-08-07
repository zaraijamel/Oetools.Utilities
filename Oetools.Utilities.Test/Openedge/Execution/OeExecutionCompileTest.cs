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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Test.Openedge.Execution {
    
    [TestClass]
    public class OeExecutionCompileTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(OeExecutionCompileTest)));

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
        public void OeExecution_Expect_Exception() {
            /*
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = false;
            using (var exec = new OeExecutionCompile(env)) {
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionFailed, "failed");
            }
            */
        }
        
        [TestMethod]
        public void Test1() {
            var content1 = @"USING namespacerandom.*.
CLASS namespacerandom.Class1 INHERITS Class2:
END CLASS.";
            var content2 = @"CLASS namespacerandom.Class2 ABSTRACT:
END CLASS.";
            
            
        }

        private bool GetEnvExecution(out EnvExecution env) {
            if (!TestHelper.GetDlcPath(out string dlcPath)) {
                env = null;
                return false;
            }
            env = new EnvExecution {
                DlcDirectoryPath = dlcPath
            };
            return true;
        }

    }
}