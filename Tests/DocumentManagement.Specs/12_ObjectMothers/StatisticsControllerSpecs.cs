﻿using System;
using System.Net.Http;
using System.Threading.Tasks;
using Chill;
using DocumentManagement.Specs._05_TestDataBuilders;
using ExampleHost.TddDemoSpecs._12_ObjectMothers;
using FluentAssertions;
using FluentAssertions.Extensions;
using LiquidProjections;
using LiquidProjections.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Newtonsoft.Json;
using Raven.Client.Documents;
using Xunit;

namespace DocumentManagement.Specs._12_ObjectMothers
{
    namespace StatisticsControllerSpecs
    {
        public class Given_a_raven_projector_with_an_in_memory_event_source : GivenWhenThen
        {
            protected Given_a_raven_projector_with_an_in_memory_event_source()
            {
                Given(async () =>
                {
                    UseThe(new MemoryEventSource());

                    SetThe<IDocumentStore>().To(new RavenDocumentStoreBuilder().Build());

                    var projector = new CountsProjector(new Dispatcher(The<MemoryEventSource>().Subscribe),
                        () => The<IDocumentStore>().OpenAsyncSession());

                    await projector.Start();

                    var webHostBuilder = new WebHostBuilder()
                        .Configure(b => b.UseStatistics(The<IDocumentStore>().OpenAsyncSession));

                    UseThe(new TestServer(webHostBuilder));
                    UseThe(The<TestServer>().CreateClient());
                });
            }

            protected EventFactory The => A;

            protected EventFactory A => new EventFactory(async @event =>
            {
                await The<MemoryEventSource>().Write(@event);
            });

        }

        public class When_a_contract_is_active : Given_a_raven_projector_with_an_in_memory_event_source
        {
            readonly Guid countryCode = Guid.NewGuid();

            public When_a_contract_is_active()
            {
                Given(async () =>
                {
                    await A.Country("Netherlands").Was.RegisteredAs(countryCode);
                    await A.Contract("123").OfKind("Filming").InCountry(countryCode).Was.Negotiated();
                    await The.Contract("123").Was.ApprovedForThePeriod(1.January(2016), DateTime.Now.Add(1.Days()));
                });

                When(async () =>
                {
                    await The.Contract("123").Is.TransitionedTo("Active");
                });
            }

            [Fact]
            public async Task Then_it_should_count_that_contract_as_a_live_document()
            {
                HttpResponseMessage response = await The<HttpClient>().GetAsync(
                    $"http://localhost/Statistics/CountsPerState?country={countryCode}&kind=Filming");

                string body = await response.Content.ReadAsStringAsync();

                var expectation = new[]
                {
                    new
                    {
                        Country = countryCode.ToString(),
                        CountryName = "Netherlands",
                        Kind = "Filming",
                        State = "Active",
                        Count = 1
                    }
                };

                object counters = JsonConvert.DeserializeAnonymousType(body, expectation);

                counters.Should().BeEquivalentTo(expectation);            }
        }
    }
}