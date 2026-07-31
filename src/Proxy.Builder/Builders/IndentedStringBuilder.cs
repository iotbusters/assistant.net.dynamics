using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assistant.Net.Dynamics.Builders
{
    /// <summary>
    ///     String builder with defined indent.
    /// </summary>
    public class IndentedStringBuilder
    {
        private const int IndentSize = 4;

        private readonly SourceBuilder sourceBuilder;
        private readonly int indent;

        private bool addIndent = true;

        /// <summary />
        public IndentedStringBuilder(SourceBuilder sourceBuilder, int indent)
        {
            this.sourceBuilder = sourceBuilder;
            this.indent = indent;
        }

        private StringBuilder Builder => sourceBuilder.StringBuilder;
        internal HashSet<string> Imports => sourceBuilder.Imports;

        /// <summary>
        ///     Appends <paramref name="sections"/> to the string builder.
        /// </summary>
        public IndentedStringBuilder Append(params string[] sections)
        {
            if (addIndent)
            {
                Builder.Append(string.Empty.PadLeft(indent * IndentSize));
                addIndent = false;
            }

            AppendJoin(string.Empty, sections);
            return this;
        }

        /// <summary>
        ///     Appends <paramref name="sections"/> separated with <paramref name="separator"/> to the string builder.
        /// </summary>
        public IndentedStringBuilder AppendJoin(string separator, string[] sections)
        {
            if (sections.Any())
            {
                Builder.Append(sections.First());
                foreach (var section in sections.Skip(1))
                    Builder.Append(separator).Append(section);
            }

            return this;
        }

        /// <summary>
        ///     Appends <paramref name="sections"/> separated with <paramref name="separator"/> to the string builder.
        /// </summary>
        public IndentedStringBuilder AppendJoin<T>(string separator, T[] sections, Action<IndentedStringBuilder, T> buildSection)
        {
            if (!sections.Any())
                return this;

            buildSection(this, sections.First());
            foreach (var section in sections.Skip(1))
            {
                Append(separator);
                buildSection(this, section);
            }
            return this;
        }

        /// <summary>
        ///     Appends <paramref name="sections"/> and default line break to the string builder.
        /// </summary>
        public IndentedStringBuilder AppendLine(params string[] sections)
        {
            Append(sections);
            Builder.AppendLine();
            addIndent = true;
            return this;
        }

        /// <summary>
        ///     Creates a new string builder with incremented indent.
        /// </summary>
        public IndentedStringBuilder Indent() => new(sourceBuilder, indent + 1);

        /// <inheritdoc/>
        public override string ToString() => Builder.ToString();
    }
}