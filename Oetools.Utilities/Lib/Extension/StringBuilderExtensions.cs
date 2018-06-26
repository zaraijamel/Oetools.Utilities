﻿#region header

// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (Extensions.cs) is part of csdeployer.
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

#endregion

using System.Text;

namespace Oetools.Utilities.Lib.Extension {

    /// <summary>
    /// This class regroups all the extension methods
    /// </summary>
    public static class StringBuilderExtensions {
        
        /// <summary>
        /// handle all whitespace chars not only spaces, trim both leading and trailing whitespaces, remove extra whitespaces,
        /// and all whitespaces are replaced to space char (so we have uniform space separator)
        /// </summary>
        /// <param name="sb"></param>
        public static StringBuilder CompactWhitespaces(this StringBuilder sb) {
            if (sb == null)
                return null;
            if (sb.Length == 0)
                return sb;

            // set [start] to first not-whitespace char or to sb.Length
            int start = 0;
            while (start < sb.Length) {
                if (char.IsWhiteSpace(sb[start]))
                    start++;
                else
                    break;
            }

            // if [sb] has only whitespaces, then return empty string
            if (start == sb.Length) {
                sb.Length = 0;
                return sb;
            }

            // set [end] to last not-whitespace char
            int end = sb.Length - 1;
            while (end >= 0) {
                if (char.IsWhiteSpace(sb[end]))
                    end--;
                else
                    break;
            }

            // compact string
            int dest = 0;
            bool previousIsWhitespace = false;
            for (int i = start; i <= end; i++) {
                if (char.IsWhiteSpace(sb[i])) {
                    if (!previousIsWhitespace) {
                        previousIsWhitespace = true;
                        sb[dest] = ' ';
                        dest++;
                    }
                } else {
                    previousIsWhitespace = false;
                    sb[dest] = sb[i];
                    dest++;
                }
            }

            sb.Length = dest;
            return sb;
        }
        
        /// <summary>
        /// Trim the end space characters in the string builder (can be limited to maxOccurences)
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="maxOccurence"></param>
        /// <returns></returns>
        public static StringBuilder TrimEnd(this StringBuilder builder, int maxOccurence = 0) {
            int occurence = 0;
            if (builder.Length > 0) {
                int i;
                for (i = builder.Length - 1; i >= 0; i--) {
                    if (!char.IsWhiteSpace(builder[i]))
                        break;
                    occurence++;
                    if (maxOccurence > 0 && occurence > maxOccurence)
                        break;
                }
                builder.Length = i + 1;
            }
            return builder;
        }
    }
}