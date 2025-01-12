using System;
using System.Collections.Generic;
using Spectre.Console;
using Vertical.SpectreLogger.Destructuring;
using Vertical.SpectreLogger.Formatting;
using Vertical.SpectreLogger.Internal;
using Vertical.SpectreLogger.Options;
using Vertical.SpectreLogger.Templates;

namespace Vertical.SpectreLogger.Output
{
    /// <summary>
    /// Extends the <see cref="IWriteBuffer"/> interface.
    /// </summary>
    public static class WriteBufferExtensions
    {
        private static readonly char[] MarkupChars = { '[', ']' };
        
        /// <summary>
        /// Writes a newline to the buffer.
        /// </summary>
        /// <param name="buffer">Buffer</param>
        public static void WriteLine(this IWriteBuffer buffer)
        {
            buffer.Write(Environment.NewLine);                
        }

        /// <summary>
        /// Writes a template state value, considering it may be a FormattedLogValues instance.
        /// </summary>
        /// <param name="buffer">Buffer</param>
        /// <param name="profile">Log level profile</param>
        /// <param name="destructureValues">Whether to destructure values</param>
        /// <param name="state">Value to evaluate</param>
        public static void WriteTemplateValue(
            this IWriteBuffer buffer,
            LogLevelProfile profile,
            bool destructureValues,
            object? state)
        {
            var template = destructureValues ? TemplateSegment.DestructureTemplate : null;
            
            if (state is not IReadOnlyList<KeyValuePair<string, object>> formattedLogValues)
            {
                buffer.WriteLogValue(profile, template, state ?? NullValue.Default);
                return;
            }

            if (!formattedLogValues.TryGetValue("{OriginalFormat}", out var originalFormat))
            {
                buffer.WriteLogValue(profile, template, state);
                return;
            }

            if (originalFormat is not string originalFormatString)
            {
                buffer.WriteLogValue(profile, template, state);
                return;
            }

            if (originalFormatString.IndexOfAny(MarkupChars) != -1)
            {
                // Save allocation at the expense of searching
                originalFormatString = Markup.Escape(originalFormatString);
            }

            TemplateString.Split(originalFormatString, (in TemplateSegment segment) =>
            {
                if (segment.IsTemplate && formattedLogValues.TryGetValue(segment.Key!, out var logValue))
                {
                    buffer.WriteLogValue(profile, segment, logValue ?? NullValue.Default);
                    return;
                }
                
                buffer.Write(segment.Value);
            });
        }

        /// <summary>
        /// Writes a log value to the buffer.
        /// </summary>
        /// <param name="buffer">Write buffer</param>
        /// <param name="profile">The profile that contains the styles and formatting to apply</param>
        /// <param name="templateSegment">The template segment</param>
        /// <param name="value">The value to write</param>
        /// <param name="writer">Optional action that commits values to the buffer</param>
        /// <typeparam name="T">The value type</typeparam>
        public static void WriteLogValue<T>(
            this IWriteBuffer buffer,
            LogLevelProfile profile,
            TemplateSegment? templateSegment,
            T value,
            Action<string>? writer = null)
            where T : notnull
        {
            if (templateSegment?.HasDestructureSpecifier == true)
            {
                DestructuringWriter.Write(buffer, profile, value);
                return;
            }
            
            var closeTag = WriteOpenMarkupTag(buffer, profile, value);
            var format = templateSegment?.CompositeFormatSpan ?? string.Empty;
            var formatString = "{0" + format + "}";
            var valueFormatted = string.Format(
                profile.FormatProvider,
                formatString,
                value);

            if (writer != null)
            {
                writer(valueFormatted.EscapeMarkup());
            }
            else
            {
                buffer.Write(valueFormatted.EscapeMarkup());
            }

            if (closeTag != null)
            {
                buffer.Write(closeTag);
            }
        }

        /// <summary>
        /// Writes a log value to the buffer, only applying style.
        /// </summary>
        /// <param name="buffer">Write buffer</param>
        /// <param name="profile">The profile that contains the styles and formatting to apply</param>
        /// <param name="value">The value to write</param>
        /// <typeparam name="T">The value type</typeparam>
        public static void WriteStyledValue<T>(
            this IWriteBuffer buffer,
            LogLevelProfile profile,
            T value)
            where T : notnull
        {
            var markup =
                profile.TypeStyles.GetValueOrDefault(typeof(T), null)
                ??
                profile.DefaultLogValueStyle;

            if (markup != null)
            {
                buffer.Write(markup);
            }
            
            buffer.Write(value.ToString() ?? string.Empty);

            if (markup != null)
            {
                buffer.Write("[/]");
            }
        }

        private static string? WriteOpenMarkupTag<T>(
            IWriteBuffer buffer,
            LogLevelProfile profile, 
            T value) 
            where T : notnull
        {
            var markup =
                profile.ValueStyles.GetValueOrDefault(value, null)
                ??
                profile.TypeStyles.GetValueOrDefault(value.GetType(), null)
                ??
                profile.DefaultLogValueStyle;

            if (markup == null)
                return null;
            
            buffer.Write(markup);

            return "[/]";
        }
    }
}