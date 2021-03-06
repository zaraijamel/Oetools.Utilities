﻿#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (Resources.cs) is part of Oetools.Utilities.
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

using System.IO;
using System.Reflection;

namespace Oetools.Utilities.Test.Resources {
    internal static class Resources {
        public static byte[] GetBytesFromResource(string resourcePath) {
            using (Stream resFilestream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{nameof(Oetools)}.{nameof(Utilities)}.{nameof(Test)}.{nameof(Resources)}.{resourcePath}")) {
                if (resFilestream == null) {
                    return null;
                }
                var output = new byte[resFilestream.Length];
                resFilestream.Read(output, 0, output.Length);
                return output;
            }
        }
    }
}
