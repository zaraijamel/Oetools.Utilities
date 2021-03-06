﻿#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (UoeExecutionHandleCompilation.cs) is part of Oetools.Utilities.
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Openedge.Execution.Exceptions;
using Oetools.Utilities.Resources;

namespace Oetools.Utilities.Openedge.Execution {

    public abstract class AUoeExecutionHandleCompilation : UoeExecution {

        /// <summary>
        /// Immediately stop the compilation process when a compilation error (include warnings) is found,
        /// the remaining files will not get compiled
        /// </summary>
        public bool StopOnCompilationError { get; set; }

        /// <summary>
        /// Immediately stop the compilation process when a compilation warning is found,
        /// the remaining files will not get compiled
        /// </summary>
        public bool StopOnCompilationWarning { get; set; }

        /// <summary>
        ///     When true, we activate the log just before compiling with FileId active + we use xref list the referenced
        ///     tables/sequences of the .r
        /// </summary>
        /// <remarks>
        /// we use xref and not xml-xref because :
        /// - the file is smaller
        /// - its faster to read
        /// - xref-xml has a strong tendency to fail to generate!
        /// </remarks>
        public bool CompileInAnalysisMode { get; set; }

        /// <summary>
        /// A "cheapest" analysis mode where we don't compute the database references from the xref file but use the
        /// RCODE-INFO:TABLE-LIST instead
        /// Less accurate since it won't list referenced sequences or referenced tables in LIKE TABLE statements
        /// </summary>
        public bool AnalysisModeSimplifiedDatabaseReferences { get; set; }

        /// <summary>
        /// Activate the debug list compile option, preprocess the file replacing preproc variables and includes and printing the result
        /// each line is numbered
        /// </summary>
        public bool CompileWithDebugList { get; set; }

        /// <summary>
        /// Activate the xref compile option
        /// </summary>
        public bool CompileWithXref { get; set; }

        /// <summary>
        /// Activate the listing compile option
        /// </summary>
        public bool CompileWithListing { get; set; }

        /// <summary>
        /// Activate the xml-xref compile option,
        /// it is incompatible with <see cref="CompileInAnalysisMode"/> but you can analyze and generate xml-xref at the same time
        /// using <see cref="AnalysisModeSimplifiedDatabaseReferences"/>
        /// </summary>
        public bool CompileUseXmlXref { get; set; }

        /// <summary>
        /// Activate the preprocess compile option which is a listing file exactly like <see cref="CompileWithDebugList"/> except it
        /// doesn't print the line numbers
        /// </summary>
        public bool CompileWithPreprocess { get; set; }

        /// <summary>
        /// Sets the COMPILER:MULTI-COMPILE value
        /// </summary>
        public bool CompilerMultiCompile { get; set; }

        /// <summary>
        /// Extra options to add to the compile statement, for instance "MIN-SIZE = TRUE"
        /// </summary>
        public string CompileStatementExtraOptions { get; set; }

        /// <summary>
        /// OPTIONS option of COMPILE only since 11.7 : require-full-names,require-field-qualifiers,require-full-keywords
        /// </summary>
        public string CompileOptions { get; set; }

        /// <summary>
        /// List of the files to compile / run / prolint
        /// </summary>
        public PathList<UoeFileToCompile> FilesToCompile { get; set; }

        /// <summary>
        /// List of the compiled files
        /// </summary>
        public PathList<UoeCompiledFile> CompiledFiles { get; } = new PathList<UoeCompiledFile>();

        /// <summary>
        /// Total number of files to compile
        /// </summary>
        public int NumberOfFilesToCompile => FilesToCompile.Count;

        /// <summary>
        /// Number of files already treated
        /// </summary>
        public virtual int NumberOfFilesTreated => unchecked((int) (File.Exists(_progressionFilePath) ? new FileInfo(_progressionFilePath).Length : 0));

        /// <summary>
        /// Returns a percentage representation of the compilation progression (0 -> 100%)
        /// </summary>
        public virtual float CompilationProgression {
            get {
                if (NumberOfFilesToCompile == 0)
                    return 0;
                return (float) NumberOfFilesTreated / NumberOfFilesToCompile * 100;
            }
        }

        /// <summary>
        /// Path to the file containing the COMPILE output
        /// </summary>
        private string _compilationLog;

        private string _filesListPath;

        private bool _useXmlXref;

        /// <summary>
        /// Path to a file used to determine the progression of a compilation (useful when compiling multiple programs)
        /// 1 byte = 1 file treated
        /// </summary>
        private string _progressionFilePath;

        public AUoeExecutionHandleCompilation(AUoeExecutionEnv env) : base(env) {
            _filesListPath = Path.Combine(_tempDir, "files.list");
            _progressionFilePath = Path.Combine(_tempDir, "compile.progression");
            _compilationLog = Path.Combine(_tempDir, "compilation.log");
        }

        protected override void CheckParameters() {
            base.CheckParameters();
            if (FilesToCompile == null || !FilesToCompile.Any()) {
                throw new UoeExecutionParametersException("No files specified.");
            }
            if ((CompileInAnalysisMode || AnalysisModeSimplifiedDatabaseReferences) && !Env.IsProVersionHigherOrEqualTo(new Version(10, 2, 0))) {
                throw new UoeExecutionParametersException("The analysis mode (computes file and database references required to compile) is only available for openedge >= 10.2.");
            }
        }

        protected override void SetExecutionInfo() {

            _useXmlXref = CompileUseXmlXref && (!CompileInAnalysisMode || AnalysisModeSimplifiedDatabaseReferences);

            base.SetExecutionInfo();

            // for each file of the list
            var filesListcontent = new StringBuilder();

            var count = 0;
            foreach (var file in FilesToCompile) {
                if (!File.Exists(file.CompiledPath)) {
                    throw new UoeExecutionParametersException($"Can not find the source file : {file.CompiledPath.PrettyQuote()}.");
                }

                var localSubTempDir = Path.Combine(_tempDir, count.ToString());
                var baseFileName = Path.GetFileNameWithoutExtension(file.CompiledPath);

                var compiledFile = new UoeCompiledFile(file);
                if (!CompiledFiles.TryAdd(compiledFile)) {
                    continue;
                }

                // get the output directory that will be use to generate the .r (and listing debug-list...)
                if (Path.GetExtension(file.Path ?? "").Equals(UoeConstants.ExtCls)) {
                    // for *.cls files, as many *.r files are generated, we need to compile in a temp directory
                    // we need to know which *.r files were generated for each input file
                    // so each file gets his own sub tempDir
                    compiledFile.CompilationOutputDirectory = localSubTempDir;
                } else if (!string.IsNullOrEmpty(file.PreferredTargetDirectory)) {
                    compiledFile.CompilationOutputDirectory = file.PreferredTargetDirectory;
                } else {
                    compiledFile.CompilationOutputDirectory = localSubTempDir;
                }

                Utils.CreateDirectoryIfNeeded(localSubTempDir);
                Utils.CreateDirectoryIfNeeded(compiledFile.CompilationOutputDirectory);

                compiledFile.CompilationRcodeFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{UoeConstants.ExtR}");
                compiledFile.CompilationErrorsFilePath = Path.Combine(localSubTempDir, $"{baseFileName}{UoeConstants.ExtCompileErrorsLog}");
                if (CompileWithListing) {
                    compiledFile.CompilationListingFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{UoeConstants.ExtListing}");
                }

                if (CompileWithXref && !_useXmlXref) {
                    compiledFile.CompilationXrefFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{UoeConstants.ExtXref}");
                }
                if (CompileWithXref && _useXmlXref) {
                    compiledFile.CompilationXmlXrefFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{UoeConstants.ExtXrefXml}");
                }
                if (CompileWithDebugList) {
                    compiledFile.CompilationDebugListFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{UoeConstants.ExtDebugList}");
                }
                if (CompileWithPreprocess) {
                    compiledFile.CompilationPreprocessedFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{UoeConstants.ExtPreprocessed}");
                }

                compiledFile.IsAnalysisMode = CompileInAnalysisMode;

                if (CompileInAnalysisMode) {
                    compiledFile.CompilationFileIdLogFilePath = Path.Combine(localSubTempDir, $"{baseFileName}{UoeConstants.ExtFileIdLog}");
                    if (AnalysisModeSimplifiedDatabaseReferences) {
                        compiledFile.CompilationRcodeTableListFilePath = Path.Combine(compiledFile.CompilationOutputDirectory, $"{baseFileName}{UoeConstants.ExtTableList}");
                    } else if (string.IsNullOrEmpty(compiledFile.CompilationXrefFilePath)) {
                        compiledFile.CompilationXrefFilePath = Path.Combine(localSubTempDir, $"{baseFileName}{UoeConstants.ExtXref}");
                    }
                }

                // feed files list
                filesListcontent
                    .Append(file.CompiledPath.ProQuoter())
                    .Append(" ")
                    .Append(compiledFile.CompilationOutputDirectory.ProQuoter())
                    .Append(" ")
                    .Append(compiledFile.CompilationErrorsFilePath.ProQuoter())
                    .Append(" ")
                    .Append(compiledFile.CompilationListingFilePath.ProQuoter())
                    .Append(" ")
                    .Append(compiledFile.CompilationXrefFilePath.ProQuoter())
                    .Append(" ")
                    .Append(compiledFile.CompilationXmlXrefFilePath.ProQuoter())
                    .Append(" ")
                    .Append(compiledFile.CompilationDebugListFilePath.ProQuoter())
                    .Append(" ")
                    .Append(compiledFile.CompilationPreprocessedFilePath.ProQuoter())
                    .Append(" ")
                    .Append(compiledFile.CompilationFileIdLogFilePath.ProQuoter())
                    .Append(" ")
                    .Append(compiledFile.CompilationRcodeTableListFilePath.ProQuoter())
                    .AppendLine();

                count++;
            }


            File.WriteAllText(_filesListPath, filesListcontent.ToString(), Env.IoEncoding);

            SetPreprocessedVar("CompileListFilePath", _filesListPath.ProPreProcStringify());
            SetPreprocessedVar("CompileProgressionFilePath", _progressionFilePath.ProPreProcStringify());
            SetPreprocessedVar("CompileLogPath", _compilationLog.ProPreProcStringify());
            SetPreprocessedVar("IsAnalysisMode", CompileInAnalysisMode.ToString());
            SetPreprocessedVar("GetRcodeTableList", AnalysisModeSimplifiedDatabaseReferences.ToString());
            SetPreprocessedVar("ProVerHigherOrEqualTo10.2", Env.IsProVersionHigherOrEqualTo(new Version(10, 2, 0)).ToString());
            SetPreprocessedVar("UseXmlXref", _useXmlXref.ToString());
            SetPreprocessedVar("CompileStatementExtraOptions", CompileStatementExtraOptions.ProPreProcStringify().StripQuotes());
            SetPreprocessedVar("CompilerMultiCompile", CompilerMultiCompile.ToString());
            SetPreprocessedVar("ProVerHigherOrEqualTo11.7", Env.IsProVersionHigherOrEqualTo(new Version(11, 7, 0)).ToString());
            SetPreprocessedVar("CompileOptions", CompileOptions.ProPreProcStringify());
            SetPreprocessedVar("StopOnCompilationError", StopOnCompilationError.ToString());
            SetPreprocessedVar("StopOnCompilationWarning", StopOnCompilationWarning.ToString());
            SetPreprocessedVar("StopOnCompilationReturnErrorCode", UoeConstants.StopOnCompilationReturnErrorCode.ToString());
        }

        protected override void AppendProgramToRun(StringBuilder runnerProgram) {
            base.AppendProgramToRun(runnerProgram);
            runnerProgram.AppendLine(OpenedgeResources.GetOpenedgeAsStringFromResources(@"oe_execution_handle_compilation.p"));
        }

        /// <summary>
        /// Get the results for each compiled files
        /// </summary>
        protected override void GetProcessResults() {
            base.GetProcessResults();

            // end of successful execution action
            if (!ExecutionFailed) {
                try {
                    Parallel.ForEach(CompiledFiles, file => {
                        file.ReadCompilationResults(Env.IoEncoding, _processStartDir);
                        file.ComputeRequiredDatabaseReferences(Env, AnalysisModeSimplifiedDatabaseReferences);
                    });

                    // set UoeExecutionCompilationStoppedException exception
                    for (int i = 0; i < HandledExceptions.Count; i++) {
                        if (HandledExceptions[i] is UoeExecutionOpenedgeException oeException) {
                            if (oeException.ErrorNumber == UoeConstants.StopOnCompilationReturnErrorCode) {
                                HandledExceptions[i] = new UoeExecutionCompilationStoppedException {
                                    CompilationProblems = CompiledFiles.CopyWhere(f => f.CompilationProblems != null).SelectMany(f => f.CompilationProblems).Where(e => StopOnCompilationError ? e is UoeCompilationError : e is UoeCompilationWarning).ToList(),
                                    StopOnWarning = StopOnCompilationWarning
                                };
                            }
                        }
                    }
                } catch (Exception e) {
                    HandledExceptions.Add(new UoeExecutionException("Error while reading the compilation results.", e));
                }
            }
        }

    }

}
