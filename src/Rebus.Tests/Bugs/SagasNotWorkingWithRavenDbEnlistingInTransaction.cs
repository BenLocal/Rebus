using System;
using System.Threading;
using NUnit.Framework;
using Raven.Client.Embedded;
using Rebus.Persistence.InMemory;
using Rebus.RavenDb;
using Rebus.Tests.Persistence.RavenDb;

namespace Rebus.Tests.Bugs
{
    public class SagasNotWorkingWithRavenDbEnlistingInTransaction : RebusBusMsmqIntegrationTestBase
    {
        const string Queue = "test.publisher";

        [Test]
        public void ShouldWork()
        {
            var store = new EmbeddableDocumentStore
                            {
                                RunInMemory = true
                            };

            store.Initialize();

            var activator = new HandlerActivatorForTesting();
            var checker = new CheckCallsMade();
            var bus = CreateBus(Queue, activator, new InMemorySubscriptionStorage(), new RavenDbSagaPersister(store, "Sagas")).Start(1);
            activator.UseHandler(() => new TheSaga(bus, checker));
            bus.Send(new TheFirstMessage());

            Thread.Sleep(15000);
            Assert.IsTrue(checker.First, "First should be called");
            Assert.IsTrue(checker.Second, "Second should be called");
        }

        public override string GetEndpointFor(Type messageType)
        {
            return Queue;
        }

        public class TheFirstMessage
        {
        }

        public class TheSecondMessage
        {
            public Guid CorrelationId { get; set; }
        }

        public class TheSaga : Saga<TheSaga.SomeSagaData>,
                               IAmInitiatedBy<TheFirstMessage>,
                               IHandleMessages<TheSecondMessage>
        {
            private readonly IBus bus;
            private readonly CheckCallsMade checker;

            public TheSaga(IBus bus, CheckCallsMade checker)
            {
                this.bus = bus;
                this.checker = checker;
            }

            public override void ConfigureHowToFindSaga()
            {
                Incoming<TheSecondMessage>(x => x.CorrelationId).CorrelatesWith(x => x.Id);
            }

            public class SomeSagaData : ISagaData
            {
                public SomeSagaData()
                {
                    Id = Guid.NewGuid();
                }

                public Guid Id { get; set; }
                public int Revision { get; set; }
            }

            public void Handle(TheFirstMessage message)
            {
                checker.First = true;
                bus.SendLocal(new TheSecondMessage
                                  {
                                      CorrelationId = Data.Id
                                  });
            }

            public void Handle(TheSecondMessage message)
            {
                checker.Second = true;
            }
        }
    }
}