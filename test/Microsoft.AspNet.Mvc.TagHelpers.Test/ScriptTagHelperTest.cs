// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNet.Razor.Runtime.TagHelpers;
using Microsoft.Framework.Logging;
using Moq;
using Xunit;

namespace Microsoft.AspNet.Mvc.TagHelpers
{
    public class ScriptTagHelperTest
    {
        [Theory]
        [InlineData("~/blank.js")]
        [InlineData(null)]
        public async Task RunsWhenRequiredAttributesArePresent(string srcValue)
        {
            // Arrange
            var attributes = new Dictionary<string, object>
                {
                    { "asp-fallback-src", "http://www.example.com/blank.js" },
                    { "asp-fallback-test", "isavailable()" },
                };

            if (srcValue != null)
            {
                attributes.Add("src", srcValue);
            }

            var context = MakeTagHelperContext(attributes);

            var output = MakeTagHelperOutput("script");
            var logger = new Logger();

            // Act
            var helper = new ScriptTagHelper()
            {
                Logger = logger,
                FallbackSrc = "http://www.example.com/blank.js",
                FallbackTestExpression = "isavailable()",
            };

            await helper.ProcessAsync(context, output);

            // Assert
            Assert.Null(output.TagName);
            Assert.NotNull(output.Content);
            Assert.True(output.ContentSet);
            Assert.Equal(0, logger.Logged.Count);
        }

        [Theory]
        [InlineData("asp-fallback-src")]
        [InlineData("asp-fallback-test")]
        public async Task DoesNotRunWhenARequiredAttributeIsMissing(string attributeToRemove)
        {
            // Arrange
            var attributes = new Dictionary<string, object>
                {
                    { "asp-fallback-src", "http://www.example.com/blank.js" },
                    { "asp-fallback-test", "isavailable()" },
                };

            attributes.Remove(attributeToRemove);

            Assert.Equal(1, attributes.Count);

            var context = MakeTagHelperContext(attributes);

            var output = MakeTagHelperOutput("script");
            var logger = new Logger();

            // Act
            var helper = new ScriptTagHelper
            {
                Logger = logger,
            };

            await helper.ProcessAsync(context, output);

            // Assert
            Assert.NotNull(output.TagName);
            Assert.False(output.ContentSet);
        }

        [Fact]
        public async Task DoesNotRunWhenAllRequiredAttributesAreMissing()
        {
            // Arrange
            var context = MakeTagHelperContext();
            var output = MakeTagHelperOutput("script");
            var logger = new Logger();

            // Act
            var helper = new ScriptTagHelper
            {
                Logger = logger,
            };

            await helper.ProcessAsync(context, output);

            // Assert
            Assert.NotNull(output.TagName);
            Assert.False(output.ContentSet);
        }

        [Fact]
        public async Task LogsWhenARequiredAttributeIsMissing()
        {
            // Arrange
            var attributes = new Dictionary<string, object>
                {
                    { "asp-fallback-src", "http://www.example.com/blank.js" },
                };

            var context = MakeTagHelperContext(attributes);

            var output = MakeTagHelperOutput("script");
            var logger = new Logger();

            // Act
            var helper = new ScriptTagHelper
            {
                Logger = logger,
            };

            await helper.ProcessAsync(context, output);

            // Assert
            Assert.NotNull(output.TagName);
            Assert.False(output.ContentSet);

            Assert.Equal(2, logger.Logged.Count);

            Assert.Equal(LogLevel.Warning, logger.Logged[0].Item1);
            Assert.IsType<MissingAttributeLoggerStructure>(logger.Logged[0].Item2);

            var loggerData0 = (MissingAttributeLoggerStructure)logger.Logged[0].Item2;
            Assert.Equal(1, loggerData0.MissingAttributes.Count());
            Assert.Equal("asp-fallback-test", loggerData0.MissingAttributes.Single());

            Assert.Equal(LogLevel.Verbose, logger.Logged[1].Item1);
            Assert.IsAssignableFrom<ILoggerStructure>(logger.Logged[1].Item2);
            Assert.StartsWith("Skipping processing for ScriptTagHelper",
                ((ILoggerStructure)logger.Logged[1].Item2).Format());
        }

        [Fact]
        public async Task LogsWhenAllRequiredAttributesAreMissing()
        {
            // Arrange
            var context = MakeTagHelperContext();
            var output = MakeTagHelperOutput("script");
            var logger = new Logger();

            // Act
            var helper = new ScriptTagHelper
            {
                Logger = logger,
            };

            await helper.ProcessAsync(context, output);

            // Assert
            Assert.NotNull(output.TagName);
            Assert.False(output.ContentSet);

            Assert.Equal(1, logger.Logged.Count);

            Assert.Equal(LogLevel.Verbose, logger.Logged[0].Item1);
            Assert.IsAssignableFrom<ILoggerStructure>(logger.Logged[0].Item2);
            Assert.StartsWith("Skipping processing for ScriptTagHelper",
                ((ILoggerStructure)logger.Logged[0].Item2).Format());
        }

        [Fact]
        public async Task PreservesOrderOfSourceAttributesWhenRun()
        {
            // Arrange
            var context = MakeTagHelperContext(
                attributes: new Dictionary<string, object>
                {
                    { "data-extra", "something"},
                    { "src", "/blank.js"},
                    { "data-more", "else"},
                    { "asp-fallback-src", "http://www.example.com/blank.js" },
                    { "asp-fallback-test", "isavailable()" },
                });

            var output = MakeTagHelperOutput("link",
                attributes: new Dictionary<string, string>
                {
                    { "data-extra", "something"},
                    { "src", "/blank.js"},
                    { "data-more", "else"},
                });

            var logger = new Logger();

            // Act
            var helper = new ScriptTagHelper
            {
                Logger = logger,
                FallbackSrc = "~/blank.js",
                FallbackTestExpression = "http://www.example.com/blank.js",
            };

            await helper.ProcessAsync(context, output);

            // Assert
            Assert.StartsWith("<script data-extra=\"something\" src=\"/blank.js\" data-more=\"else\"", output.Content);
            Assert.Equal(0, logger.Logged.Count);
        }

        private TagHelperContext MakeTagHelperContext(
            IDictionary<string, object> attributes = null,
            string content = null)
        {
            attributes = attributes ?? new Dictionary<string, object>();

            return new TagHelperContext(attributes, Guid.NewGuid().ToString("N"), () => Task.FromResult(content));
        }

        private TagHelperOutput MakeTagHelperOutput(string tagName, IDictionary<string, string> attributes = null)
        {
            attributes = attributes ?? new Dictionary<string, string>();

            return new TagHelperOutput(tagName, attributes);
        }

        private class Logger : ILogger<ScriptTagHelper>
        {
            public List<Tuple<LogLevel, object>> Logged { get; } = new List<Tuple<LogLevel, object>>();

            public IDisposable BeginScope(object state)
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Write(LogLevel logLevel, int eventId, object state, Exception exception, Func<object, Exception, string> formatter)
            {
                Logged.Add(Tuple.Create(logLevel, state));
            }
        }
    }
}