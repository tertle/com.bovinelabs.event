// <copyright file="EventSystemBase.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Systems
{
     using Unity.Collections;
     using Unity.Entities;
     using UnityEngine.Scripting;

     public abstract class EventSystemBase : SystemBase
     {
          private NativeHashMap<long, EventContainer> eventContainers;

          /// <summary>
          /// Initializes a new instance of the <see cref="EventSystemBase"/> class.
          /// </summary>
          [Preserve]
          public EventSystemBase()
          {
               // Done in constructor so other jobs can register / deregister in OnStart()
               this.eventContainers = new NativeHashMap<long, EventContainer>(16, Allocator.Persistent);
          }

          /// <summary> Registers and allocates new event producer. </summary>
          /// <typeparam name="T"> The event type. </typeparam>
          /// <returns> The new allocated producer. </returns>
          public EventProducer<T> RegisterProducer<T>()
               where T : struct
          {
               var container = this.GetOrCreateEventContainer<T>();
               return container.CreateProducer<T>();
          }

          /// <summary> Deregisters and frees the memory of an existing event producer. </summary>
          /// <param name="producer"> The producer to deregister. </param>
          /// <typeparam name="T"> The event type. </typeparam>
          public void DeregisterProducer<T>(EventProducer<T> producer)
               where T : struct
          {
               // If container doesn't exist it's because the EventSystem has already disposed it
               var container = this.GetEventContainer<T>();
               if (container.IsValid)
               {
                    container.RemoveProducer(producer);
               }
          }

          /// <summary> Registers and allocates new event consumer. </summary>
          /// <typeparam name="T"> The event type. </typeparam>
          /// <returns> The new allocated consumer. </returns>
          public EventConsumer<T> RegisterConsumer<T>()
               where T : struct
          {
               var container = this.GetOrCreateEventContainer<T>();
               return container.CreateConsumer<T>();
          }

          /// <summary> Deregisters and frees the memory of an existing event consumer. </summary>
          /// <param name="consumer"> The consumer to deregister. </param>
          /// <typeparam name="T"> The event type. </typeparam>
          public void DeregisterConsumer<T>(EventConsumer<T> consumer)
               where T : struct
          {
               // If container doesn't exist it's because the EventSystem has already disposed it
               var container = this.GetEventContainer<T>();
               if (container.IsValid)
               {
                    container.RemoveConsumer(consumer);
               }
          }

          /// <inheritdoc/>
          protected override void OnDestroy()
          {
               using var e = this.eventContainers.GetEnumerator();
               while (e.MoveNext())
               {
                    e.Current.Value.Dispose();
               }

               this.eventContainers.Dispose();
          }

          /// <inheritdoc/>
          protected override void OnUpdate()
          {
               var containers = this.eventContainers;

               this.Job.WithCode(() =>
                    {
                         using var e = containers.GetEnumerator();
                         while (e.MoveNext())
                         {
                              e.Current.Value.Update();
                         }
                    })
                    .Run();
          }

          private EventContainer GetOrCreateEventContainer<T>()
               where T : struct
          {
               var hash = Unity.Burst.BurstRuntime.GetHashCode64<T>();

               if (!this.eventContainers.TryGetValue(hash, out var container))
               {
                    container = this.eventContainers[hash] = new EventContainer(hash);
               }

               return container;
          }

          private EventContainer GetEventContainer<T>()
               where T : struct
          {
               if (!this.eventContainers.IsCreated)
               {
                    return default;
               }

               var hash = Unity.Burst.BurstRuntime.GetHashCode64<T>();
               return this.eventContainers.TryGetValue(hash, out var container) ? container : default;
          }
     }
}