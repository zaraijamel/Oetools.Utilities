﻿using Oetools.Utilities.Archive;

namespace Oetools.Utilities.Test.Archive {
    public class FileInArchive : IFileInArchiveToExtract, IFileInArchiveToDelete, IFileToArchive, IFileInArchiveToMove {
        public string ArchivePath { get; set; }
        public string PathInArchive { get; set; }
        public bool Processed { get; set; }
        public string ExtractionPath { get; set; }
        public string SourcePath { get; set; }
        public string NewRelativePathInArchive { get; set; }
    }
}