﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging.Testing;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.Formatters
{
    public abstract class JsonInputFormatterTestBase : LoggedTest
    {
        internal enum Formatter
        {
            Newtonsoft,
            SystemText
        }

        internal abstract Formatter CurrentFormatter { get; }

        [Theory]
        [InlineData("application/json", true)]
        [InlineData("application/*", false)]
        [InlineData("*/*", false)]
        [InlineData("text/json", true)]
        [InlineData("text/*", false)]
        [InlineData("text/xml", false)]
        [InlineData("application/xml", false)]
        [InlineData("application/some.entity+json", true)]
        [InlineData("application/some.entity+json;v=2", true)]
        [InlineData("application/some.entity+xml", false)]
        [InlineData("application/some.entity+*", false)]
        [InlineData("text/some.entity+json", true)]
        [InlineData("", false)]
        [InlineData(null, false)]
        [InlineData("invalid", false)]
        public void CanRead_ReturnsTrueForAnySupportedContentType(string requestContentType, bool expectedCanRead)
        {
            // Arrange
            var formatter = GetInputFormatter();

            var contentBytes = Encoding.UTF8.GetBytes("content");
            var httpContext = GetHttpContext(contentBytes, contentType: requestContentType);

            var formatterContext = CreateInputFormatterContext(typeof(string), httpContext);

            // Act
            var result = formatter.CanRead(formatterContext);

            // Assert
            Assert.Equal(expectedCanRead, result);
        }

        [Fact]
        public void DefaultMediaType_ReturnsApplicationJson()
        {
            // Arrange
            var formatter = GetInputFormatter();

            // Act
            var mediaType = formatter.SupportedMediaTypes[0];

            // Assert
            Assert.Equal("application/json", mediaType.ToString());
        }

        [Fact]
        public async Task JsonFormatterReadsIntValue()
        {
            // Arrange
            var content = "100";
            var formatter = GetInputFormatter();

            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(int), httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.False(result.HasError);
            var intValue = Assert.IsType<int>(result.Model);
            Assert.Equal(100, intValue);
        }

        [Fact]
        public async Task JsonFormatterReadsStringValue()
        {
            // Arrange
            var content = "\"abcd\"";
            var formatter = GetInputFormatter();

            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(string), httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.False(result.HasError);
            var stringValue = Assert.IsType<string>(result.Model);
            Assert.Equal("abcd", stringValue);
        }

        [Fact]
        public async Task JsonFormatter_EscapedKeys_Bracket()
        {
            // Arrange
            var content = "[{\"It[s a key\":1234556}]";
            var formatter = GetInputFormatter();

            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(IEnumerable<IDictionary<string, short>>), httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.True(result.HasError);
            Assert.Collection(
                formatterContext.ModelState.OrderBy(k => k.Key),
                kvp =>
                {
                    Assert.Equal("[0][\'It[s a key\']", kvp.Key);
                });
        }

        [Fact]
        public async Task JsonFormatter_EscapedKeys()
        {
            // Arrange
            var content = "[{\"It\\\"s a key\": 1234556}]";
            var formatter = GetInputFormatter();

            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(
                typeof(IEnumerable<IDictionary<string, short>>), httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.True(result.HasError);
            Assert.Collection(
                formatterContext.ModelState.OrderBy(k => k.Key),
                kvp =>
                {
                    switch(CurrentFormatter)
                    {
                        case Formatter.Newtonsoft:
                            Assert.Equal("[0]['It\"s a key']", kvp.Key);
                            break;
                        case Formatter.SystemText:
                            Assert.Equal("[0][\'It\\u0022s a key\']", kvp.Key);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                });
        }

        [Fact]
        public virtual async Task JsonFormatterReadsDateTimeValue()
        {
            // Arrange
            var expected = new DateTime(2012, 02, 01, 00, 45, 00);
            var content = $"\"{expected.ToString("O")}\"";
            var formatter = GetInputFormatter();

            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(DateTime), httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.False(result.HasError);
            var dateValue = Assert.IsType<DateTime>(result.Model);
            Assert.Equal(expected, dateValue);
        }

        [Fact]
        public async Task JsonFormatterReadsComplexTypes()
        {
            // Arrange
            var formatter = GetInputFormatter();

            var content = "{\"name\": \"Person Name\", \"age\": 30}";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(ComplexModel), httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.False(result.HasError);
            var userModel = Assert.IsType<ComplexModel>(result.Model);
            Assert.Equal("Person Name", userModel.Name);
            Assert.Equal(30, userModel.Age);
        }

        [Fact]
        public async Task ReadAsync_ReadsValidArray()
        {
            // Arrange
            var formatter = GetInputFormatter();

            var content = "[0, 23, 300]";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(int[]), httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.False(result.HasError);
            var integers = Assert.IsType<int[]>(result.Model);
            Assert.Equal(new int[] { 0, 23, 300 }, integers);
        }

        [Fact]
        public virtual Task ReadAsync_ReadsValidArray_AsListOfT() => ReadAsync_ReadsValidArray_AsList(typeof(List<int>));

        [Fact]
        public virtual Task ReadAsync_ReadsValidArray_AsIListOfT() => ReadAsync_ReadsValidArray_AsList(typeof(IList<int>));

        [Fact]
        public virtual Task ReadAsync_ReadsValidArray_AsCollectionOfT() => ReadAsync_ReadsValidArray_AsList(typeof(ICollection<int>));

        [Fact]
        public virtual Task ReadAsync_ReadsValidArray_AsEnumerableOfT() => ReadAsync_ReadsValidArray_AsList(typeof(IEnumerable<int>));

        protected async Task ReadAsync_ReadsValidArray_AsList(Type requestedType)
        {
            // Arrange
            var formatter = GetInputFormatter();

            var content = "[0, 23, 300]";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(requestedType, httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.False(result.HasError);
            Assert.IsAssignableFrom(requestedType, result.Model);
            Assert.Equal(new int[] { 0, 23, 300 }, (IEnumerable<int>)result.Model);
        }

        [Fact]
        public virtual async Task ReadAsync_ArrayOfObjects_HasCorrectKey()
        {
            // Arrange
            var formatter = GetInputFormatter();

            var content = "[{\"Age\": 5}, {\"Age\": 3}, {\"Age\": \"Cheese\"} ]";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(List<ComplexModel>), httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.True(result.HasError, "Model should have had an error!");
            Assert.Single(formatterContext.ModelState["[2].Age"].Errors);
        }

        [Fact]
        public virtual async Task ReadAsync_AddsModelValidationErrorsToModelState()
        {
            // Arrange
            var formatter = GetInputFormatter();

            var content = "{ \"Name\": \"Person Name\", \"Age\": \"not-an-age\" }";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(ComplexModel), httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.True(result.HasError, "Model should have had an error!");
            Assert.Single(formatterContext.ModelState["Age"].Errors);
        }

        [Fact]
        public virtual async Task ReadAsync_InvalidArray_AddsOverflowErrorsToModelState()
        {
            // Arrange
            var formatter = GetInputFormatter();

            var content = "[0, 23, 33767]";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(short[]), httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.True(result.HasError, "Model should have produced an error!");
            Assert.True(formatterContext.ModelState.ContainsKey("[2]"), "Should have contained key '[2]'");
        }

        [Fact]
        public virtual async Task ReadAsync_InvalidComplexArray_AddsOverflowErrorsToModelState()
        {
            // Arrange
            var formatter = GetInputFormatter();

            var content = "[{ \"Name\": \"Name One\", \"Age\": 30}, { \"Name\": \"Name Two\", \"Small\": 300}]";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(ComplexModel[]), httpContext, modelName: "names");

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.True(result.HasError);
            Assert.Collection(
                formatterContext.ModelState.OrderBy(k => k.Key),
                kvp => {
                    Assert.Equal("names[1].Small", kvp.Key);
                    Assert.Single(kvp.Value.Errors);
                });
        }

        [Fact]
        public virtual async Task ReadAsync_UsesTryAddModelValidationErrorsToModelState()
        {
            // Arrange
            var formatter = GetInputFormatter();

            var content = "{ \"Name\": \"Person Name\", \"Age\": \"not-an-age\"}";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(ComplexModel), httpContext);
            formatterContext.ModelState.MaxAllowedErrors = 3;
            formatterContext.ModelState.AddModelError("key1", "error1");
            formatterContext.ModelState.AddModelError("key2", "error2");

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.True(result.HasError);

            Assert.False(formatterContext.ModelState.ContainsKey("age"));
            var error = Assert.Single(formatterContext.ModelState[""].Errors);
            Assert.IsType<TooManyModelErrorsException>(error.Exception);
        }

        [Theory]
        [InlineData("null", true, true)]
        [InlineData("null", false, false)]
        public async Task ReadAsync_WithInputThatDeserializesToNull_SetsModelOnlyIfAllowingEmptyInput(
            string content,
            bool treatEmptyInputAsDefaultValue,
            bool expectedIsModelSet)
        {
            // Arrange
            var formatter = GetInputFormatter();

            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(
                typeof(string),
                httpContext,
                treatEmptyInputAsDefaultValue: treatEmptyInputAsDefaultValue);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.False(result.HasError);
            Assert.Equal(expectedIsModelSet, result.IsModelSet);
            Assert.Null(result.Model);
        }

        [Fact]
        public async Task ReadAsync_ComplexPoco()
        {
            // Arrange
            var formatter = GetInputFormatter();

            var content = "{ \"Id\": 5, \"Person\": { \"Name\": \"name\", \"Numbers\": [3, 2, \"Hamburger\"]} }";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(ComplexPoco), httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.True(result.HasError, "Model should have had an error!");
            Assert.Single(formatterContext.ModelState["Person.Numbers[2]"].Errors);
        }

        [Fact]
        public virtual async Task ReadAsync_RequiredAttribute()
        {
            // Arrange
            var formatter = GetInputFormatter();
            var content = "{ \"Id\": 5, \"Person\": {\"Numbers\": [3]} }";
            var contentBytes = Encoding.UTF8.GetBytes(content);
            var httpContext = GetHttpContext(contentBytes);

            var formatterContext = CreateInputFormatterContext(typeof(ComplexPoco), httpContext);

            // Act
            var result = await formatter.ReadAsync(formatterContext);

            // Assert
            Assert.True(result.HasError, "Model should have had an error!");
            Assert.Single(formatterContext.ModelState["Person.Name"].Errors);
        }

        protected abstract TextInputFormatter GetInputFormatter();

        protected static HttpContext GetHttpContext(
            byte[] contentBytes,
            string contentType = "application/json")
        {
            return GetHttpContext(new MemoryStream(contentBytes), contentType);
        }

        protected static HttpContext GetHttpContext(
            Stream requestStream,
            string contentType = "application/json")
        {
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Body = requestStream;
            httpContext.Request.ContentType = contentType;

            return httpContext;
        }

        protected static InputFormatterContext CreateInputFormatterContext(
            Type modelType,
            HttpContext httpContext,
            string modelName = null,
            bool treatEmptyInputAsDefaultValue = false)
        {
            var provider = new EmptyModelMetadataProvider();
            var metadata = provider.GetMetadataForType(modelType);

            return new InputFormatterContext(
                httpContext,
                modelName: modelName ?? string.Empty,
                modelState: new ModelStateDictionary(),
                metadata: metadata,
                readerFactory: new TestHttpRequestStreamReaderFactory().CreateReader,
                treatEmptyInputAsDefaultValue: treatEmptyInputAsDefaultValue);
        }

        protected sealed class ComplexPoco
        {
            public int Id { get; set; }
            public Person Person{ get; set; }
        }

        protected sealed class Person
        {
            [Required]
            [JsonProperty(Required = Required.Always)]
            public string Name { get; set; }
            public IEnumerable<int> Numbers { get; set; }
        }

        protected sealed class ComplexModel
        {
            public string Name { get; set; }

            public decimal Age { get; set; }

            public byte Small { get; set; }
        }
    }
}
