﻿#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecution.cs) is part of Oetools.Utilities.
//
// Oetools.Utilities is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Oetools.Utilities is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Execution.Exceptions;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Execution {

    /// <summary>
    /// Base class for all the progress execution (i.e. when we need to start a prowin process and do something)
    /// </summary>
    public abstract class UoeExecution : IDisposable {

        /// <summary>
        /// The action to execute just after the end of a prowin process
        /// </summary>
        public event Action<UoeExecution> OnExecutionEnd;

        /// <summary>
        /// The action to execute at the end of the process if it went well
        /// </summary>
        public event Action<UoeExecution> OnExecutionOk;

        /// <summary>
        /// The action to execute at the end of the process if something went wrong
        /// </summary>
        public event Action<UoeExecution> OnExecutionException;

        /// <summary>
        /// Set to true if a valid database connection is mandatory (if so, the execution will stop if the db fails to connnect)
        /// </summary>
        public virtual bool NeedDatabaseConnection { get; set; }

        /// <summary>
        /// Set the openedge working directory (would default to <see cref="ExecutionTemporaryDirectory"/>)
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Environment to use
        /// </summary>
        public AUoeExecutionEnv Env { get; }

        /// <summary>
        /// Cancellation token.
        /// </summary>
        public CancellationToken? CancelToken {
            get => _cancelToken;
            set {
                _cancelToken = value;
                if (_process != null) {
                    _process.CancelToken = _cancelToken;
                }
            }
        }

        /// <summary>
        /// Set to true if a the execution process has been killed
        /// </summary>
        public bool HasBeenKilled { get; protected set; }

        /// <summary>
        /// Set to true after the process is over if there was errors during the execution.
        /// The process executed till the end but there were errors or warning that should be displayed
        /// to the end user.
        /// You can check <see cref="HandledExceptions"/> to get the errors.
        /// </summary>
        public bool ExecutionHandledExceptions => HandledExceptions != null && HandledExceptions.Count > 0;

        /// <summary>
        /// Set to true if the process failed to go to the end or didn't event start,
        /// if this is true, you should be really worried because something is wrong with the internal
        /// procedures in this very library.
        /// You can check <see cref="HandledExceptions"/> to get the errors.
        /// </summary>
        public bool ExecutionFailed { get; protected set; }

        /// <summary>
        /// Set to true after the process is over if the database connection has failed
        /// </summary>
        public bool DatabaseConnectionFailed { get; protected set; }

        /// <summary>
        /// Total amount of time needed for this execution
        /// </summary>
        public TimeSpan? ExecutionTimeSpan { get; protected set; }

        /// <summary>
        /// Start time for this execution
        /// </summary>
        public DateTime? StartDateTime { get; protected set; }

        /// <summary>
        /// List of handled exceptions :
        /// - <see cref="UoeExecutionException"/>
        /// - <see cref="UoeExecutionParametersException"/>
        /// - <see cref="UoeExecutionOpenedgeException"/>
        /// - <see cref="UoeExecutionOpenedgeDbConnectionException"/>
        /// </summary>
        public List<UoeExecutionException> HandledExceptions { get; } = new List<UoeExecutionException>();

        /// <summary>
        /// Temporary directory used for the execution
        /// </summary>
        public string ExecutionTemporaryDirectory => _tempDir;

        /// <summary>
        /// if the exe should be executed silently (hidden) or not
        /// </summary>
        protected virtual bool SilentExecution => true;

        /// <summary>
        /// can only be executed with the gui version of progres (e.g. windows only)
        /// </summary>
        protected virtual bool RequiresGraphicalMode => false;

        /// <summary>
        /// forces to use the character mode of progres (imcompatible with <see cref="RequiresGraphicalMode"/>
        /// </summary>
        protected virtual bool ForceCharacterModeUse => false;

        protected readonly Dictionary<string, string> PreprocessedVars;

        /// <summary>
        /// Path to the output progress .log file (errors that happened when running the process).
        /// </summary>
        protected string _errorLogPath;

        /// <summary>
        /// Path to the database connection log (not existing if everything is ok).
        /// </summary>
        protected string _dbErrorLogPath;

        /// <summary>
        /// Full path to the directory containing all the files needed for the execution.
        /// </summary>
        protected string _tempDir;

        /// <summary>
        /// Full path to the directory used as the working directory to start the prowin process.
        /// </summary>
        protected string _processStartDir;

        protected string _propathFilePath;

        /// <summary>
        /// Parameters of the .exe call
        /// </summary>
        protected ProcessArgs _exeArguments;

        protected UoeProcessIo _process;

        protected string _runnerPath;

        protected bool _executed;

        protected bool _eventPublished;
        private CancellationToken? _cancelToken;

        /// <summary>
        /// The process is considered to be running when it is <see cref="Started"/> and not <see cref="Ended"/>
        /// </summary>
        internal bool Started { get; set; }

        internal bool Ended { get; set; }

        /// <summary>
        ///     Deletes temp directory and everything in it
        /// </summary>
        public virtual void Dispose() {
            try {
                _process?.Dispose();

                // delete temp dir
                if (_tempDir != null) {
                    Utils.DeleteDirectoryIfExists(_tempDir, true);
                }
            } catch (Exception e) {
                HandledExceptions.Add(new UoeExecutionException("Error when disposing of the process.", e));
            }
        }

        public UoeExecution(AUoeExecutionEnv env) {
            Env = env;
            PreprocessedVars = new Dictionary<string, string>();

            // unique temporary folder
            _tempDir = Path.Combine(Env.TempDirectory, $"exec_{Utils.GetRandomName()}");
            _errorLogPath = Path.Combine(_tempDir, "run.errors");
            _dbErrorLogPath = Path.Combine(_tempDir, "db.errors");
            _propathFilePath = Path.Combine(_tempDir, "oe.propath");
            _runnerPath = Path.Combine(_tempDir, $"run_{DateTime.Now:HHmmssfff}.p");
            _processStartDir = _tempDir;
        }

        /// <summary>
        /// Starts the execution and does not wait for its ending.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="UoeExecutionException"></exception>
        public void ExecuteNoWait() {
            StartDateTime = DateTime.Now;
            try {
                ExecuteNoWaitInternal();
            } catch (Exception) {
                Ended = true;
                _eventPublished = true; // to not block the wait for the exit
                throw;
            } finally {
                Started = true;
            }
        }

        /// <summary>
        ///     allows to prepare the execution environment by creating a unique temp folder
        ///     and copying every critical files into it
        ///     Then execute the progress program
        /// </summary>
        /// <returns></returns>
        /// <exception cref="UoeExecutionException"></exception>
        protected virtual void ExecuteNoWaitInternal() {
            // check parameters
            CheckParameters();

            Utils.CreateDirectoryIfNeeded(_tempDir);

            // write propath
            var propath = $"{_tempDir},{(Env.ProPathList != null ? string.Join(",", Env.ProPathList) : "")}\n";
            if (propath.Length > UoeConstants.MaximumPropathLength) {
                // TODO : find a better working directory that would shorten the propath
                throw new UoeExecutionParametersException($"The propath used is too long (>{UoeConstants.MaximumPropathLength}) : {propath.PrettyQuote()}.");
            }

            File.WriteAllText(_propathFilePath, propath, Env.IoEncoding);

            // Set info
            SetExecutionInfo();
            SetPreprocessedVar("ErrorLogPath", _errorLogPath.ProPreProcStringify());
            SetPreprocessedVar("DbErrorLogPath", _dbErrorLogPath.ProPreProcStringify());
            SetPreprocessedVar("PropathFilePath", _propathFilePath.ProPreProcStringify());
            SetPreprocessedVar("DbConnectString", new UoeProcessArgs().Append(Env.DatabaseConnections).ToQuotedArgs().ProPreProcStringify());
            SetPreprocessedVar("DatabaseAliasList", (Env.DatabaseAliases != null ? string.Join(";", Env.DatabaseAliases.Select(a => $"{a.AliasLogicalName},{a.DatabaseLogicalName}")) : "").ProPreProcStringify()); // Format : ALIAS,DATABASE;ALIAS2,DATABASE;...
            SetPreprocessedVar("DbConnectionRequired", NeedDatabaseConnection.ToString());
            SetPreprocessedVar("PreExecutionProgramPath", Env.PreExecutionProgramPath.ProPreProcStringify());
            SetPreprocessedVar("PostExecutionProgramPath", Env.PostExecutionProgramPath.ProPreProcStringify());
            var userCharacterModeOfProgress = ForceCharacterModeUse || Env.UseProgressCharacterMode && !RequiresGraphicalMode;
            SetPreprocessedVar("TryToHideProcessFromTaskBarOnWindows", (Utils.IsRuntimeWindowsPlatform && SilentExecution && !userCharacterModeOfProgress && Env.TryToHideProcessFromTaskBarOnWindows).ToString());


            // prepare the .p runner
            var runnerProgram = new StringBuilder();
            foreach (var var in PreprocessedVars) {
                runnerProgram.AppendLine($"&SCOPED-DEFINE {var.Key} {var.Value}");
            }

            runnerProgram.AppendLine(OpenedgeResources.GetOpenedgeAsStringFromResources(@"oe_execution.p"));
            AppendProgramToRun(runnerProgram);
            File.WriteAllText(_runnerPath, runnerProgram.ToString(), Env.IoEncoding);

            // Parameters
            _exeArguments = new ProcessArgs();
            _exeArguments.Append("-p").Append(_runnerPath);
            AppendProgressParameters(_exeArguments);
            if (Env.ProExeCommandLineParameters != null) {
                _exeArguments.Append(Env.ProExeCommandLineParameters);
            }

            if (!string.IsNullOrEmpty(Env.IniFilePath)) {
                _exeArguments.Append("-ininame").Append(Env.IniFilePath).Append("-basekey").Append("INI");
            }

            if (!string.IsNullOrEmpty(WorkingDirectory) && Directory.Exists(WorkingDirectory)) {
                _processStartDir = WorkingDirectory;
                _exeArguments.Append("-T").Append(_tempDir);
            }

            // start the process
            _process = new UoeProcessIo(Env.DlcDirectoryPath, userCharacterModeOfProgress, Env.CanProVersionUseNoSplash) {
                WorkingDirectory = _processStartDir,
                RedirectedOutputEncoding = Env.IoEncoding,
                CancelToken = CancelToken
            };
            _process.OnProcessExit += ProcessOnExited;
            _process.ExecuteNoWait(_exeArguments, SilentExecution);
        }

        /// <summary>
        /// Allows to kill the process of this execution
        /// </summary>
        public virtual void KillProcess() {
            if (StartDateTime == null) {
                return;
            }
            if (!HasBeenKilled) {
                HasBeenKilled = true;
                // wait for the execution to start and then kill it
                SpinWait.SpinUntil(() => Started, 10000);
                try {
                    _process?.Kill();
                } catch (Exception e) {
                    HandledExceptions.Add(new UoeExecutionException("Error when killing the process.", e));
                }
            }
        }

        /// <summary>
        /// Synchronously wait for the execution to end
        /// Returns true if the process has exited (can be false if timeout was reached)
        /// </summary>
        /// <param name="maxWaitInMs"></param>
        public virtual bool WaitForExit(int maxWaitInMs = 0) {
            if (!Started || _process == null) {
                return true;
            }

            if (maxWaitInMs > 0) {
                var exited = _process.WaitForExit(maxWaitInMs);
                if (!exited) {
                    return false;
                }
            } else {
                _process.WaitForExit();
            }

            // wait for the execution to really end
            SpinWait.SpinUntil(() => _eventPublished, 10000);

            return true;
        }

        /// <summary>
        /// Starts the execution and wait for its ending (or timeout).
        /// </summary>
        /// <param name="maxWaitInMs"></param>
        /// <returns></returns>
        public virtual bool Execute(int maxWaitInMs = 0) {
            ExecuteNoWait();
            return WaitForExit(maxWaitInMs);
        }

        /// <summary>
        /// Throws an exception if this execution caught exceptions.
        /// </summary>
        /// <exception cref="UoeExecutionGlobalException"></exception>
        public virtual void ThrowIfExceptionsCaught() {
            if (ExecutionFailed) {
                throw new UoeExecutionGlobalException(HandledExceptions);
            }
            if (ExecutionHandledExceptions) {
                throw new UoeExecutionGlobalException(HandledExceptions);
            }
        }

        /// <summary>
        /// Should throw an error if some parameters are incorrect
        /// </summary>
        /// <exception cref="UoeExecutionException"></exception>
        protected virtual void CheckParameters() {
            if (_executed) {
                throw new UoeExecutionException("This process has already been executed, you can't start it again.");
            }
            _executed = true;

            if (!Directory.Exists(Env.DlcDirectoryPath)) {
                throw new UoeExecutionParametersException($"Can not start an execution, the DLC directory does not exist : {Env.DlcDirectoryPath.PrettyQuote()}.");
            }
        }

        /// <summary>
        /// Method that appends the program_to_run procedure to the run .p file
        /// </summary>
        /// <param name="runnerProgram"></param>
        protected virtual void AppendProgramToRun(StringBuilder runnerProgram) {}

        /// <summary>
        /// Extra stuff to do before executing
        /// </summary>
        /// <exception cref="UoeExecutionException"></exception>
        protected virtual void SetExecutionInfo() { }

        /// <summary>
        /// Add stuff to the command line
        /// </summary>
        protected virtual void AppendProgressParameters(ProcessArgs args) { }

        /// <summary>
        /// Set pre-processed variable for the runner program
        /// </summary>
        protected void SetPreprocessedVar(string key, string value) {
            if (!PreprocessedVars.ContainsKey(key))
                PreprocessedVars.Add(key, value);
            else
                PreprocessedVars[key] = value;
        }

        /// <summary>
        /// Called by the process's thread when it is over, execute the ProcessOnExited event
        /// </summary>
        private void ProcessOnExited(object sender, EventArgs eventArgs) {
            try {
                GetProcessResults();
            } catch (Exception e) {
                HandledExceptions.Add(new UoeExecutionException("Error when checking the process results.", e));
            } finally {
                PublishEndEvents();
            }
        }

        protected virtual void GetProcessResults() {
            Ended = true;
            ExecutionTimeSpan = TimeSpan.FromMilliseconds(DateTime.Now.Subtract(StartDateTime ?? DateTime.Now).TotalMilliseconds);
            HasBeenKilled = HasBeenKilled || _process.Killed;

            // if the db log file exists, then the connect statement failed
            if (File.Exists(_dbErrorLogPath)) {
                HandledExceptions.AddRange(UoeExecutionOpenedgeException.GetFromTabbedLogFile<UoeExecutionOpenedgeDbConnectionException>(_dbErrorLogPath, Env.IoEncoding));
                DatabaseConnectionFailed = true;
                ExecutionFailed = NeedDatabaseConnection;
                if (HandledExceptions.FirstOrDefault(e => {
                    if (e is UoeExecutionOpenedgeException oeEx) {
                        return oeEx.ErrorNumber == 748;
                    }
                    return false;
                }) is UoeExecutionOpenedgeException tooMuchConnectionOnDbException) {
                    tooMuchConnectionOnDbException.ErrorMessage = $"{tooMuchConnectionOnDbException.ErrorMessage} This message indicates that too many connections were done on a database, increase the maximum number of connections in the proserve command (or use proserve if the database is used in single user mode).";
                }
            }

            if (HasBeenKilled) {
                HandledExceptions.Add(new UoeExecutionKilledException());
                ExecutionFailed = true;
            } else if (_process.ExitCode > 0 || !File.Exists(_errorLogPath)) {
                // exit code not 0 or the log file wasn't created, indicating that the procedure didn't run until the end correctly
                foreach (var output in _process.StandardOutputArray.Union(_process.ErrorOutputArray)) {
                    var ex = UoeExecutionOpenedgeException.GetFromString(output);
                    if (ex != null) {
                        HandledExceptions.Add(ex);
                    }
                }

                if (HandledExceptions.Count == 0) {
                    HandledExceptions.Add(new UoeExecutionProcessException(_process.ExecutablePath, _process.UsedArguments, _process.WorkingDirectory, _process.BatchOutput.ToString(), _process.ExitCode));
                }
                ExecutionFailed = true;
            } else if (new FileInfo(_errorLogPath).Length > 0) {
                // else if the log isn't empty, something went wrong
                HandledExceptions.AddRange(UoeExecutionOpenedgeException.GetFromTabbedLogFile<UoeExecutionOpenedgeException>(_errorLogPath, Env.IoEncoding));
                ExecutionFailed = false;
            } else if (_process.StandardOutputArray.Count > 0 || _process.ErrorOutputArray.Count > 0) {
                // we do not put anything in the standard output (PUT), if there is something then it is a runtime error!
                // however, the execution went until the end
                foreach (var output in _process.StandardOutputArray.Union(_process.ErrorOutputArray)) {
                    var ex = UoeExecutionOpenedgeException.GetFromString(output);
                    if (ex != null) {
                        HandledExceptions.Add(ex);
                    }
                }
            }
        }

        /// <summary>
        /// publish the end of execution events
        /// </summary>
        protected void PublishEndEvents() {
            // end of successful/unsuccessful execution action
            try {
                if (ExecutionHandledExceptions) {
                    OnExecutionException?.Invoke(this);
                } else {
                    OnExecutionOk?.Invoke(this);
                }
            } catch (Exception e) {
                HandledExceptions.Add(new UoeExecutionException("Error in published events.", e));
            }

            // end of execution action
            try {
                OnExecutionEnd?.Invoke(this);
            } catch (Exception e) {
                HandledExceptions.Add(new UoeExecutionException("Error in published events end.", e));
            }
            _eventPublished = true;
        }

    }
}
