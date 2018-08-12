﻿// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (FileToCompile.cs) is part of csdeployer.
// 
// csdeployer is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// csdeployer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with csdeployer. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Openedge.Execution {
    
    /// <summary>
    ///     This class represents a file thas been compiled
    /// </summary>
    public class CompiledFile {
        /// <summary>
        ///     The path to the source that has been compiled
        /// </summary>
        public string SourceFilePath { get; }

        /// <summary>
        /// The path of the file that actually needs to be compiled
        /// (can be different from sourcepath if we edited it without saving it for instance)
        /// </summary>
        public string CompiledFilePath { get; }

        public string CompilationOutputDirectory { get; set; }

        public string CompilationErrorsFilePath { get; set; }
        public string CompilationListingFilePath { get; set; }
        public string CompilationXrefFilePath { get; set; }
        public string CompilationXmlXrefFilePath { get; set; }
        public string CompilationDebugListFilePath { get; set; }
        public string CompilationPreprocessedFilePath { get; set; }

        /// <summary>
        /// This temporary file is actually a log with only FileId activated just before the compilation
        /// and deactivated just after; this allows us to know which file were used to compile the source
        /// </summary>
        /// <remarks>
        /// Why don't we use the INCLUDE lines in the .xref file?
        /// because it is not directly a file path,
        /// it the content of what is between {an include}, which means it is a relative path (from PROPATH)
        /// and it can contains all the parameters... It easier to use this method
        /// also because we would need to analyse CLASS lines to know if the .cls depends on others...
        /// Lot of work, FILEID is the easy road
        /// </remarks>
        public string CompilationFileIdLogFilePath { get; set; }
        
        /// <summary>
        /// Will contain the list of table/tCRC referenced by the compiled rcode
        /// uses RCODE-INFO:TABLE-LIST to get a list of referenced TABLES in the file,
        /// this list of tables does not include referenced table in statements like :
        /// - DEF VAR efzef LIKE DB.TABLE
        /// - DEF TEMP-TABLE zefezf LIKE DB.TABLE
        /// and so on...
        /// If a table listed here is modified, the source file should be recompiled or, at runtime, you would have a bad CRC error
        /// Note : when you refer a sequence, the TABLE-LIST will have an entry like : DATABASENAME._Sequence
        /// </summary>
        public string CompilationRcodeTableListFilePath { get; set; }

        public string CompilationRcodeFilePath { get; set; }
        
        public bool IsAnalysisMode { get; set; }
        
        public bool CompiledCorrectly { get; private set; }
        
        /// <summary>
        ///     List of errors
        /// </summary>
        public List<CompilationError> CompilationErrors { get; set; }

        /// <summary>
        ///     represents the source file (i.e. includes) used to generate a given .r code file
        /// </summary>
        public HashSet<string> RequiredFiles { get; private set; }

        /// <summary>
        /// represent the tables or sequences that were referenced in a given .r code file and thus needed to compile
        /// also, if one reference changes, the file should be recompiled
        /// it is list of DATABASENAME.TABLENAME or DATABASENAME.SEQUENCENAME, you should probably verify
        /// that those references do exist afterward and also get the TABLE CRC value
        /// </summary>
        public List<DatabaseReference> RequiredDatabaseReferences { get; private set; }
        
        /// <summary>
        ///     Returns the base file name (set in constructor)
        /// </summary>
        public string BaseFileName { get; }


        /// <summary>
        ///     Constructor
        /// </summary>
        public CompiledFile(FileToCompile fileToCompile) {
            SourceFilePath = fileToCompile.SourcePath;
            CompiledFilePath = fileToCompile.CompiledPath;
            BaseFileName = Path.GetFileNameWithoutExtension(SourceFilePath);
        }

        private bool _compilationResultsRead;
        
        public void ReadCompilationResults() {
            if (_compilationResultsRead) {
                return;
            }

            _compilationResultsRead = true;
            
            // make sure that the expected generated files are actually generated
            AddWarningIfFileDefinedButDoesNotExist(CompilationListingFilePath);
            AddWarningIfFileDefinedButDoesNotExist(CompilationXrefFilePath);
            AddWarningIfFileDefinedButDoesNotExist(CompilationXmlXrefFilePath);
            AddWarningIfFileDefinedButDoesNotExist(CompilationDebugListFilePath);
            AddWarningIfFileDefinedButDoesNotExist(CompilationPreprocessedFilePath);
            
            CorrectRcodePathForClassFiles();

            // read compilation errors/warning for this file
            ReadCompilationErrors();

            if (IsAnalysisMode) {
                AddWarningIfFileDefinedButDoesNotExist(CompilationFileIdLogFilePath);
                ComputeReferencedFiles();
            }

            CompiledCorrectly = File.Exists(CompilationRcodeFilePath) && (CompilationErrors == null || CompilationErrors.Count == 0);
        }
        
        /// <summary>
        /// Read the table referenced in the source file using either the xref file or the RCODE-INFO:TABLE-LIST,
        /// make sure to also get the CRC value for each table
        /// </summary>
        /// <param name="ienv"></param>
        public void ComputeRequiredDatabaseReferences(IEnvExecution ienv) {
            if (!IsAnalysisMode || string.IsNullOrEmpty(CompilationXrefFilePath) && string.IsNullOrEmpty(CompilationRcodeTableListFilePath)) {
                return;
            }
            AddWarningIfFileDefinedButDoesNotExist(CompilationRcodeTableListFilePath);
            
            RequiredDatabaseReferences = new List<DatabaseReference>();
            
            // read from xref (we need the table CRC from the environment)
            if (ienv is EnvExecution env) {
                if (File.Exists(CompilationXrefFilePath)) {
                    foreach (var dbRef in ProUtilities.GetDatabaseReferencesFromXrefFile(CompilationXrefFilePath)) {
                        if (env.TablesCrc.ContainsKey(dbRef)) {
                            RequiredDatabaseReferences.Add(new DatabaseReferenceTable {
                                QualifiedName = dbRef,
                                Crc = env.TablesCrc[dbRef]
                            });
                        } else if (env.Sequences.Contains(dbRef)) {
                            RequiredDatabaseReferences.Add(new DatabaseReferenceSequence {
                                QualifiedName = dbRef
                            });
                        }
                    }
                }
            }
            
            // read from rcode-info:table-list
            if (File.Exists(CompilationRcodeTableListFilePath)) {
                Utils.ForEachLine(CompilationRcodeTableListFilePath, null, (i, line) => {
                    var split = line.Split('\t');
                    if (split.Length >= 1) {
                        var qualifiedName = split[0].Trim();
                        if (!RequiredDatabaseReferences.Exists(r => r.QualifiedName.EqualsCi(qualifiedName))) {
                            RequiredDatabaseReferences.Add(new DatabaseReferenceTable {
                                QualifiedName = qualifiedName,
                                Crc = split[1].Trim()
                            });
                        }
                    }
                }, Encoding.Default);
            }
        }

        private void AddWarningIfFileDefinedButDoesNotExist(string path) {
            if (!string.IsNullOrEmpty(path) && !File.Exists(path)) {
                (CompilationErrors ?? (CompilationErrors = new List<CompilationError>())).Add(new CompilationError {
                    SourcePath = SourceFilePath,
                    Column = 1,
                    Line = 1,
                    Level = CompilationErrorLevel.Warning,
                    ErrorNumber = 0,
                    Message = $"{path} has not been generated"
                });
            }
        }

        private void ReadCompilationErrors() {
            if (!string.IsNullOrEmpty(CompilationErrorsFilePath) && File.Exists(CompilationErrorsFilePath)) {
                Utils.ForEachLine(CompilationErrorsFilePath, null, (i, line) => {
                    var fields = line.Split('\t');
                    if (fields.Length == 7) {
                        var error = new CompilationError {
                            SourcePath = fields[1].Equals(CompiledFilePath) ? SourceFilePath : fields[1],
                            Line = Math.Max(0, (int) fields[3].ConvertFromStr(typeof(int))),
                            Column = Math.Max(0, (int) fields[4].ConvertFromStr(typeof(int))),
                            ErrorNumber = Math.Max(0, (int) fields[5].ConvertFromStr(typeof(int)))
                        };

                        if (!Enum.TryParse(fields[2], true, out CompilationErrorLevel compilationErrorLevel))
                            compilationErrorLevel = CompilationErrorLevel.Error;
                        error.Level = compilationErrorLevel;

                        error.Message = fields[6].ProUnescapeString().Replace(CompiledFilePath, SourceFilePath).Trim();

                        (CompilationErrors ?? (CompilationErrors = new List<CompilationError>())).Add(error);
                    }
                });
            }
        }

        private void CorrectRcodePathForClassFiles() {
            
            // this only concerns cls files
            if (SourceFilePath.EndsWith(OeConstants.ExtCls, StringComparison.CurrentCultureIgnoreCase)) {
                // Handle the case of .cls files, for which several .r code are compiled
                // if the file we compiled implements/inherits from another class, there is more than 1 *.r file generated.
                // Moreover, they are generated in their respective package folders

                // for each *.r file in the compilation output directory
                foreach (var rCodeFilePath in Directory.EnumerateFiles(CompilationOutputDirectory, $"*{OeConstants.ExtR}", SearchOption.AllDirectories)) {
                    // if this is actually the .cls file we want to compile, the .r file isn't necessary directly in the compilation dir like we expect,
                    // it can be in folders corresponding to the package of the class
                    if (BaseFileName.Equals(Path.GetFileNameWithoutExtension(rCodeFilePath))) {
                        // correct .r path
                        CompilationRcodeFilePath = rCodeFilePath;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Gets the files that were necessary to compile this file
        /// </summary>
        private void ComputeReferencedFiles() {

            if (string.IsNullOrEmpty(CompilationFileIdLogFilePath)) {
                return;
            }
            
            if (File.Exists(CompilationFileIdLogFilePath)) {
                RequiredFiles = ProUtilities.GetReferencedFilesFromFileIdLog(CompilationFileIdLogFilePath, Encoding.Default);
                
                RequiredFiles.RemoveWhere(f => 
                    f.EqualsCi(CompiledFilePath) ||
                    f.EndsWith(".r", StringComparison.CurrentCultureIgnoreCase) ||
                    f.EndsWith(".pl", StringComparison.CurrentCultureIgnoreCase) ||
                    !String.IsNullOrEmpty(CompilationXrefFilePath) && f.EqualsCi(CompilationXrefFilePath));
                
            }
        }

    }

}