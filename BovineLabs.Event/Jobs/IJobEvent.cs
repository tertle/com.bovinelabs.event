// <copyright file="IJobEvent.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Jobs
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Systems;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    /// <summary> Job that visits each event. </summary>
    /// <typeparam name="T"> Type of event. </typeparam>
    [JobProducerType(typeof(JobEvent.JobEventStructParallelChain<,>))]
    [SuppressMessage("ReSharper", "TypeParameterCanBeVariant", Justification = "Strict requirements for compiler")]
    public interface IJobEvent<T>
        where T : struct
    {
        /// <summary> Executes the next event. </summary>
        /// <param name="e"> The event. </param>
        void Execute(T e);
    }

    /// <summary> Extension methods for <see cref="IJobEvent{T}"/> . </summary>
    public static class JobEvent
    {
        /// <summary> Schedule a <see cref="IJobEvent{T}"/> job. </summary>
        /// <param name="jobData"> The job. </param>
        /// <param name="eventSystem"> The event system. </param>
        /// <param name="dependsOn"> T he job handle dependency. </param>
        /// <typeparam name="TJob"> The type of the job. </typeparam>
        /// <typeparam name="T"> The type of the key in the hash map. </typeparam>
        /// <returns> The handle to job. </returns>
        public static unsafe JobHandle ScheduleParallel<TJob, T>(
            this TJob jobData,
            EventSystemBase eventSystem,
            JobHandle dependsOn = default)
            where TJob : struct, IJobEvent<T>
            where T : struct
        {
            dependsOn = eventSystem.GetEventReaders<T>(dependsOn, out var events);

            for (var i = 0; i < events.Count; i++)
            {
                var reader = events[i];

                var fullData = new JobEventStructParallelChain<TJob, T>
                {
                    Reader = reader,
                    JobData = jobData,
                };

                var scheduleParams = new JobsUtility.JobScheduleParameters(
                    UnsafeUtility.AddressOf(ref fullData),
                    JobEventStructParallelChain<TJob, T>.Initialize(),
                    dependsOn,
                    ScheduleMode.Batched);

                dependsOn = JobsUtility.ScheduleParallelFor(
                    ref scheduleParams,
                    reader.ForEachCount,
                    1);
            }

            eventSystem.AddJobHandleForConsumer<T>(dependsOn);

            return dependsOn;
        }

        /// <summary> The job execution struct. </summary>
        /// <typeparam name="TJob"> The type of the job. </typeparam>
        /// <typeparam name="T"> The type of the event. </typeparam>
        internal struct JobEventStructParallelChain<TJob, T>
            where TJob : struct, IJobEvent<T>
            where T : struct
        {
            /// <summary> The <see cref="NativeThreadStream.Reader"/> . </summary>
            [ReadOnly]
            public NativeThreadStream.Reader Reader;

            /// <summary> The job. </summary>
            public TJob JobData;

            // ReSharper disable once StaticMemberInGenericType
            private static IntPtr jobReflectionData;

            private delegate void ExecuteJobFunction(
                ref JobEventStructParallelChain<TJob, T> fullData,
                IntPtr additionalPtr,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex);

            /// <summary> Initializes the job. </summary>
            /// <returns> The job pointer. </returns>
            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                {
                    jobReflectionData = JobsUtility.CreateJobReflectionData(
                        typeof(JobEventStructParallelChain<TJob, T>),
                        typeof(TJob),
                        JobType.ParallelFor,
                        (ExecuteJobFunction)Execute);
                }

                return jobReflectionData;
            }

            /// <summary> Executes the job. </summary>
            /// <param name="fullData"> The job data. </param>
            /// <param name="additionalPtr"> AdditionalPtr. </param>
            /// <param name="bufferRangePatchData"> BufferRangePatchData. </param>
            /// <param name="ranges"> The job range. </param>
            /// <param name="jobIndex"> The job index. </param>
            [SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "Required by burst.")]
            public static void Execute(
                ref JobEventStructParallelChain<TJob, T> fullData,
                IntPtr additionalPtr,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex)
            {
                while (true)
                {
                    if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out var begin, out var end))
                    {
                        return;
                    }

                    for (int i = begin; i < end; i++)
                    {
                        var count = fullData.Reader.BeginForEachIndex(begin);

                        for (var j = 0; j < count; j++)
                        {
                            var e = fullData.Reader.Read<T>();
                            fullData.JobData.Execute(e);
                        }

                        fullData.Reader.EndForEachIndex();
                    }
                }
            }
        }
    }
}