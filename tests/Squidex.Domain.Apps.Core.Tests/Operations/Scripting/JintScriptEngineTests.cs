﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschränkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using System.Security.Claims;
using Squidex.Domain.Apps.Core.Contents;
using Squidex.Domain.Apps.Core.Scripting;
using Squidex.Infrastructure;
using Squidex.Infrastructure.Security;
using Xunit;

namespace Squidex.Domain.Apps.Core.Operations.Scripting
{
    public class JintScriptEngineTests
    {
        private readonly JintScriptEngine scriptEngine = new JintScriptEngine { Timeout = TimeSpan.FromSeconds(1) };

        [Fact]
        public void Should_throw_validation_exception_when_calling_reject()
        {
            Assert.Throws<ValidationException>(() => scriptEngine.Execute(new ScriptContext(), "reject()"));
            Assert.Throws<ValidationException>(() => scriptEngine.Execute(new ScriptContext(), "reject('Not valid')"));
        }

        [Fact]
        public void Should_throw_security_exception_when_calling_reject()
        {
            Assert.Throws<DomainForbiddenException>(() => scriptEngine.Execute(new ScriptContext(), "disallow()"));
            Assert.Throws<DomainForbiddenException>(() => scriptEngine.Execute(new ScriptContext(), "disallow('Not allowed')"));
        }

        [Fact]
        public void Should_catch_script_syntax_errors()
        {
            Assert.Throws<ValidationException>(() => scriptEngine.Execute(new ScriptContext(), "x => x"));
        }

        [Fact]
        public void Should_catch_script_runtime_errors()
        {
            Assert.Throws<ValidationException>(() => scriptEngine.Execute(new ScriptContext(), "throw 'Error';"));
        }

        [Fact]
        public void Should_catch_script_runtime_errors_on_execute_and_transform()
        {
            Assert.Throws<ValidationException>(() => scriptEngine.ExecuteAndTransform(new ScriptContext(), "throw 'Error';"));
        }

        [Fact]
        public void Should_return_original_content_when_transform_script_failed()
        {
            var content = new NamedContentData();
            var context = new ScriptContext { Data = content };

            var result = scriptEngine.Transform(context, "x => x");

            Assert.Same(content, result);
        }

        [Fact]
        public void Should_throw_when_execute_and_transform_script_failed()
        {
            var content = new NamedContentData();
            var context = new ScriptContext { Data = content };

            Assert.Throws<ValidationException>(() => scriptEngine.ExecuteAndTransform(context, "x => x"));
        }

        [Fact]
        public void Should_return_original_content_when_content_is_not_replaced()
        {
            var content = new NamedContentData();
            var context = new ScriptContext { Data = content };

            var result = scriptEngine.ExecuteAndTransform(context, "var x = 0;");

            Assert.Same(content, result);
        }

        [Fact]
        public void Should_fetch_operation_name()
        {
            var content = new NamedContentData();

            var expected =
                new NamedContentData()
                    .AddField("operation",
                        new ContentFieldData()
                            .AddValue("iv", "MyOperation"));

            var context = new ScriptContext { Data = content, Operation = "MyOperation" };

            var result = scriptEngine.ExecuteAndTransform(context, @"
                var data = ctx.data;

                data.operation = { iv: ctx.operation };

                replace(data)");

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Should_transform_content_and_return_with_transform()
        {
            var content =
                new NamedContentData()
                    .AddField("number0",
                        new ContentFieldData()
                            .AddValue("iv", 1.0))
                    .AddField("number1",
                        new ContentFieldData()
                            .AddValue("iv", 1.0));
            var expected =
                new NamedContentData()
                    .AddField("number1",
                        new ContentFieldData()
                            .AddValue("iv", 2.0))
                    .AddField("number2",
                        new ContentFieldData()
                            .AddValue("iv", 10.0));

            var context = new ScriptContext { Data = content };

            var result = scriptEngine.Transform(context, @"
                var data = ctx.data;

                delete data.number0;

                data.number1.iv = data.number1.iv + 1;
                data.number2 = { 'iv': 10 };

                replace(data);");

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Should_slugify_value()
        {
            var content =
                new NamedContentData()
                    .AddField("title",
                        new ContentFieldData()
                            .AddValue("iv", "Hello World"));

            var expected =
                new NamedContentData()
                    .AddField("title",
                        new ContentFieldData()
                            .AddValue("iv", "Hello World"))
                    .AddField("slug",
                        new ContentFieldData()
                            .AddValue("iv", "hello-world"));

            var context = new ScriptContext { Data = content };

            var result = scriptEngine.Transform(context, @"
                var data = ctx.data;

                data.slug = { iv: slugify(data.title.iv) };

                replace(data);");

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Should_transform_content_and_return_with_execute_transform()
        {
            var content =
                new NamedContentData()
                    .AddField("number0",
                        new ContentFieldData()
                            .AddValue("iv", 1.0))
                    .AddField("number1",
                        new ContentFieldData()
                            .AddValue("iv", 1.0));
            var expected =
                new NamedContentData()
                    .AddField("number1",
                        new ContentFieldData()
                            .AddValue("iv", 2.0))
                    .AddField("number2",
                        new ContentFieldData()
                            .AddValue("iv", 10.0));

            var context = new ScriptContext { Data = content };

            var result = scriptEngine.ExecuteAndTransform(context, @"
                var data = ctx.data;

                delete data.number0;

                data.number1.iv = data.number1.iv + 1;
                data.number2 = { 'iv': 10 };

                replace(data);");

            Assert.Equal(expected, result);
        }

        [Fact]
        public void Should_transform_content_with_old_content()
        {
            var content =
                new NamedContentData()
                    .AddField("number0",
                        new ContentFieldData()
                            .AddValue("iv", 3.0));

            var oldContent =
                new NamedContentData()
                    .AddField("number0",
                        new ContentFieldData()
                            .AddValue("iv", 5.0));

            var expected =
                new NamedContentData()
                    .AddField("number0",
                        new ContentFieldData()
                            .AddValue("iv", 13.0));

            var userIdentity = new ClaimsIdentity();
            var userPrincipal = new ClaimsPrincipal(userIdentity);

            userIdentity.AddClaim(new Claim(OpenIdClaims.ClientId, "2"));

            var context = new ScriptContext { Data = content, OldData = oldContent, User = userPrincipal };

            var result = scriptEngine.ExecuteAndTransform(context, @"
                ctx.data.number0.iv = ctx.data.number0.iv + ctx.oldData.number0.iv * parseInt(ctx.user.id, 10);

                replace(ctx.data);");

            Assert.Equal(expected, result);
        }
    }
}
