﻿#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (Exceptions.cs) is part of Oetools.Utilities.
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

namespace Oetools.Utilities.Archive.Ftp.Core {
    /*
     *  Copyright 2008 Alessandro Pilotti
     *
     *  This program is free software; you can redistribute it and/or modify
     *  it under the terms of the GNU Lesser General Public License as published by
     *  the Free Software Foundation; either version 2.1 of the License, or
     *  (at your option) any later version.
     *
     *  This program is distributed in the hope that it will be useful,
     *  but WITHOUT ANY WARRANTY; without even the implied warranty of
     *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
     *  GNU Lesser General Public License for more details.
     *
     *  You should have received a copy of the GNU Lesser General Public License
     *  along with this program; if not, write to the Free Software
     *  Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA  02110-1301  USA 
     */

    /// <summary>
    ///     Base FTP exception class.
    /// </summary>
    internal class FtpException : Exception {
        protected FtpException() { }

        public FtpException(string message)
            : base(message) { }

        public FtpException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    internal class FtpReplyParseException : FtpException {
        public string ReplyText { get; private set; }

        public FtpReplyParseException(string replyText)
            : base("Invalid server reply: " + replyText) {
            ReplyText = replyText;
        }
    }

    internal class FtpProtocolException : FtpException {
        public FtpReply Reply { get; private set; }

        public FtpProtocolException(FtpReply reply)
            : base("Invalid FTP protocol reply: " + reply) {
            Reply = reply;
        }
    }

    /// <summary>
    ///     Exception indicating that a command or set of commands have been cancelled by the caller, via a callback method or
    ///     event.
    /// </summary>
    internal class FtpOperationCancelledException : FtpException {
        public FtpOperationCancelledException(string message)
            : base(message) { }
    }

    /// <summary>
    ///     FTP exception generated by a command with a return code >= 400, as stated in RFC 959.
    /// </summary>
    internal class FtpCommandException : FtpException {
        public int ErrorCode { get; private set; }

        public FtpCommandException(string message)
            : base(message) { }

        public FtpCommandException(string message, Exception innerException)
            : base(message, innerException) { }

        public FtpCommandException(FtpReply reply)
            : base(reply.Message) {
            ErrorCode = reply.Code;
        }

        public override string Message => $"{base.Message} ({ErrorCode})";
    }

    /// <summary>
    ///     FTP exception related to the SSL/TLS support
    /// </summary>
    internal class FtpSslException : FtpException {
        public FtpSslException(string message)
            : base(message) { }

        public FtpSslException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}