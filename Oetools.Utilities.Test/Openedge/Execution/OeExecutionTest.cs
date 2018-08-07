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

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Database;
using Oetools.Utilities.Openedge.Execution;

namespace Oetools.Utilities.Test.Openedge.Execution {
    
    [TestClass]
    public class OeExecutionTest {
        
        private static string _testFolder;

        private static string TestFolder => _testFolder ?? (_testFolder = TestHelper.GetTestFolder(nameof(OeExecutionTest)));

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
            Assert.ThrowsException<ExecutionParametersException>(() => {
                using (var exec = new OeExecutionCustomTest(new EnvExecution())) {
                    exec.Start();
                    exec.WaitForProcessExit();
                }
            });
        }
        
        [TestMethod]
        [DataRow(false, "gui")]
        [DataRow(true, "tty")]
        public void OeExecution_Test_DisplayType_Ok(bool useProgressCharacterMode, string expected) {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = useProgressCharacterMode;
            using (var exec = new OeExecutionCustomTest(env)) {
                exec.ProgramContent = "PUT UNFORMATTED SESSION:DISPLAY-TYPE.";
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionFailed, "failed");
                Assert.IsFalse(exec.HasBeenKilled, "killed");
                Assert.AreEqual(expected, exec.Output.ToLower(), "checking DISPLAY-TYPE");
            }
        }

        private int _iOeExecutionTestEvents;
        
        [TestMethod]
        public void OeExecution_Test_Events() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = true;
            using (var exec = new OeExecutionCustomTest(env)) {
                exec.OnExecutionEnd += execution => _iOeExecutionTestEvents++;
                exec.OnExecutionOk += execution => _iOeExecutionTestEvents = _iOeExecutionTestEvents + 2;
                exec.OnExecutionFailed += execution => _iOeExecutionTestEvents = _iOeExecutionTestEvents + 4;
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionFailed, "ok");
                Assert.AreEqual(3, _iOeExecutionTestEvents);

                _iOeExecutionTestEvents = 0;
                exec.ProgramContent = "to fail";
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsTrue(exec.ExecutionFailed, "failed");
                Assert.AreEqual(5, _iOeExecutionTestEvents);
            }
        }
        
        [TestMethod]
        public void OeExecution_Test_Killed() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = true;
            using (var exec = new OeExecutionCustomTest(env)) {
                exec.ProgramContent = "PAUSE 10.";
                exec.Start();
                Task.Factory.StartNew(() => {
                    Thread.Sleep(1000);
                    exec.KillProcess();
                });
                exec.WaitForProcessExit();
                Assert.IsTrue(exec.ExecutionFailed, "failed");
                Assert.IsTrue(exec.HasBeenKilled, "failed");
            }
        }
        
        [TestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void OeExecution_Test_Errors(bool useCharMode) {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = useCharMode;
            using (var exec = new OeExecutionCustomTest(env)) {
                // exit code > 0
                exec.ProgramContent = "compilation error!!";
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsTrue(exec.ExecutionFailed, "failed");
                Assert.IsTrue(exec.HandledExceptions.Exists(e => e is ExecutionProcessException));
                
                exec.ProgramContent = "";
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionFailed, "not failed");
                
                // error in log
                exec.ProgramContent = "return error \"oups\".";
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsTrue(exec.ExecutionFailed, "failed2");
                Assert.IsTrue(exec.HandledExceptions.Exists(e => e is ExecutionOpenedgeException e1 && e1.ErrorMessage.Equals("oups")));
                
                // runtime error (this won't work in non batch mode)
                exec.ProgramContent = @"
                DEFINE VARIABLE lc_1 AS CHARACTER NO-UNDO.

                DEFINE VARIABLE li_i AS INTEGER NO-UNDO.
                    DO li_i = 1 TO 33000:
                ASSIGN lc_1 = lc_1 + ""a"".
                    END.";
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionFailed, "not failed2");
                Assert.IsTrue(exec.HandledExceptions.Exists(e => e is ExecutionOpenedgeException e1 && e1.ErrorNumber > 0));
            }

            env.ProExeCommandLineParameters = "random derp";
            using (var exec = new OeExecutionCustomTest(env)) {
                // error in command line
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsTrue(exec.ExecutionFailed, "failed3");
                Assert.IsTrue(exec.HandledExceptions.Exists(e => e is ExecutionProcessException));
            }
        }
        
        [TestMethod]
        public void OeExecution_Test_DbConnection_ok() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = true;
            
            // generate temp base
            var db = new DatabaseOperator(env.DlcDirectoryPath);
            var dbPn = "test1.db";
            var stPath = db.CreateStandardStructureFile(Path.Combine(TestFolder, dbPn));
            db.ProstrctCreate(Path.Combine(TestFolder, dbPn), stPath, DatabaseBlockSize.S1024);
            db.Procopy(Path.Combine(TestFolder, dbPn), DatabaseBlockSize.S1024);
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, dbPn)));
            
            // generate temp base
            var dbPn2 = "test2.db";
            stPath = db.CreateStandardStructureFile(Path.Combine(TestFolder, dbPn2));
            db.ProstrctCreate(Path.Combine(TestFolder, dbPn2), stPath, DatabaseBlockSize.S1024);
            db.Procopy(Path.Combine(TestFolder, dbPn2), DatabaseBlockSize.S1024);
            Assert.IsTrue(File.Exists(Path.Combine(TestFolder, dbPn2)));
            
            // try if connected well and can manage aliases
            env.DatabaseConnectionString = $"{DatabaseOperator.GetConnectionString(Path.Combine(TestFolder, dbPn))} {DatabaseOperator.GetConnectionString(Path.Combine(TestFolder, dbPn2))}";
            env.DatabaseAliases = new List<IEnvExecutionDatabaseAlias> {
                new EnvExecutionDatabaseAlias {
                    DatabaseLogicalName = "test1",
                    AliasLogicalName = "alias1"
                },
                new EnvExecutionDatabaseAlias {
                    DatabaseLogicalName = "test1",
                    AliasLogicalName = "alias2"
                },
                new EnvExecutionDatabaseAlias {
                    DatabaseLogicalName = "test2",
                    AliasLogicalName = "alias3"
                }
            };
            using (var exec = new OeExecutionCustomTest(env)) {
                exec.NeedDatabaseConnection = true;
                exec.ProgramContent = @"
                DEFINE VARIABLE li_db AS INTEGER NO-UNDO.
                REPEAT li_db = 1 TO NUM-ALIASES:
                    PUT UNFORMATTED ALIAS(li_db) SKIP.
                END.";
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionFailed, "ok");
                Assert.AreEqual("dictdb alias1 alias2 alias3", exec.Output.CliCompactWhitespaces());
            }
        }
        
        [TestMethod]
        public void OeExecution_Test_DbConnection_fail() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = true;
            
            // try if connected well and can manage aliases
            env.DatabaseConnectionString = DatabaseOperator.GetConnectionString(Path.Combine(TestFolder, "random.db"));
            using (var exec = new OeExecutionCustomTest(env)) {
                exec.NeedDatabaseConnection = true;
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsTrue(exec.ExecutionFailed, "failed");
                Assert.IsTrue(exec.DbConnectionFailed, "no connection");
                
                exec.NeedDatabaseConnection = false;
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionFailed, "ok");
                Assert.IsTrue(exec.DbConnectionFailed, "no connection 2");
            }
        }
        
        [TestMethod]
        public void OeExecution_Test_CmdLineOptions() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = true;
            env.ProExeCommandLineParameters = "-s 2000";
            using (var exec = new OeExecutionCustomTest(env)) {
                exec.ProgramContent = "PUT UNFORMATTED SESSION:STARTUP-PARAMETERS.";
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionFailed, "ok");
                Assert.IsTrue(exec.Output.Contains("-s 2000"), "startup params");
            }
        }
        
        [TestMethod]
        public void OeExecution_Test_Propath() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = true;
            env.ProPathList = new List<string> { TestFolder, Path.Combine(TestFolder, "random") };
            using (var exec = new OeExecutionCustomTest(env)) {
                exec.ProgramContent = "PUT UNFORMATTED PROPATH.";
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionFailed, "ok");
                Assert.IsTrue(exec.Output.Contains(TestFolder), "propath 1");
                Assert.IsTrue(exec.Output.Contains(Path.Combine(TestFolder, "random")), "propath 2");
            }
        }
        
        [TestMethod]
        public void OeExecution_Test_Ini() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = false;
            env.IniFilePath = Path.Combine(TestFolder, "test.ini");
            File.WriteAllText(env.IniFilePath, @"
            [Colors]
            color0=1,0,0
            ");
            using (var exec = new OeExecutionCustomTest(env)) {
                exec.ProgramContent = "PUT UNFORMATTED COLOR-TABLE:GET-RGB-VALUE(0).";
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionFailed, "ok");
                Assert.AreEqual("1", exec.Output);
            }
        }
        
        [TestMethod]
        public void OeExecution_Test_PrePostExecutionProgramPath() {
            if (!GetEnvExecution(out EnvExecution env)) {
                return;
            }
            env.UseProgressCharacterMode = true;
            env.PostExecutionProgramPath = Path.Combine(TestFolder, "prog.p");
            File.WriteAllText(Path.Combine(TestFolder, "prog.p"), @"PUT UNFORMATTED ""okay"".");
            using (var exec = new OeExecutionCustomTest(env)) {
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionFailed, "ok");
                Assert.AreEqual("okay", exec.Output);
            }

            env.PostExecutionProgramPath = null;
            env.PreExecutionProgramPath = Path.Combine(TestFolder, "prog.p");
            using (var exec = new OeExecutionCustomTest(env)) {
                exec.Start();
                exec.WaitForProcessExit();
                Assert.IsFalse(exec.ExecutionFailed, "ok");
                Assert.AreEqual("okay", exec.Output);
            }
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


        private class OeExecutionCustomTest : OeExecution {
            
            public string ProgramContent { get; set; }
            
            public string Output => _process.BatchOutput.ToString();
            
            public OeExecutionCustomTest(IEnvExecution env) : base(env) { }

            protected override void AppendProgramToRun(StringBuilder runnerProgram) {
                runnerProgram.AppendLine("PROCEDURE program_to_run PRIVATE:");
                if (!string.IsNullOrEmpty(ProgramContent)) {
                    runnerProgram.AppendLine(ProgramContent);
                }
                runnerProgram.AppendLine("END PROCEDURE.");
            }
        }
    }
}