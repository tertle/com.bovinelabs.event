// <copyright file="IJobEventReaderForEach.cs" company="BovineLabs">
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
    using Unity.Entities;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    /// <summary> Job that visits each event stream. </summary>
    /// <typeparam name="T"> Type of event. </typeparam>
    [JobProducerType(typeof(JobEventReaderForEach.JobEventReaderForEachStructParallel<,>))]
    [SuppressMessage("ReSharper", "TypeParameterCanBeVariant", Justification = "Strict requirements for compiler")]
    [SuppressMessage("ReSharper", "UnusedTypeParameter", Justification = "Required by scheduler")]
    public interface IJobEventReaderForEach<T>
        where T : unmanaged
    {
        /// <summary> Executes the next event. </summary>
        /// <param name="stream"> The stream. </param>
        /// <param name="foreachIndex"> The foreach index. </param>
        void Execute(NativeEventStream.Reader stream, int foreachIndex);
    }

    /// <summary> Extension methods for <see cref="IJobEventReaderForEach{T}"/> . </summary>
    public static class JobEventReaderForEach
    {
        /// <summary> Schedule a <see cref="IJobEventReaderForEach{T}"/> job. </summary>
        /// <param name="jobData"> The job. </param>
        /// <param name="eventSystem"> The event system. </param>
        /// <param name="dependsOn"> The job handle dependency. </param>
        /// <typeparam name="TJob"> The type of the job. </typeparam>
        /// <typeparam name="T"> The type of the key in the hash map. </typeparam>
        /// <returns> The handle to job. </returns>
        public static unsafe JobHandle ScheduleParallel<TJob, T>(
            this TJob jobData, EventSystemBase eventSystem, JobHandle dependsOn = default)
            where TJob : struct, IJobEventReaderForEach<T>
            where T : unmanaged
        {
            dependsOn = eventSystem.GetEventReaders<T>(dependsOn, out var events);

            for (var i = 0; i < events.Count; i++)
            {
                var reader = events[i];

                var fullData = new JobEventReaderForEachStructParallel<TJob, T>
                {
                    Reader = reader,
                    JobData = jobData,
                    Index = i,
                };

#if UNITY_2020_2_OR_NEWER
                const ScheduleMode scheduleMode = ScheduleMode.Parallel;
#else
                const ScheduleMode scheduleMode = ScheduleMode.Batched;
#endif

                var scheduleParams = new JobsUtility.JobScheduleParameters(
                    UnsafeUtility.AddressOf(ref fullData),
                    JobEventReaderForEachStructParallel<TJob, T>.Initialize(),
                    dependsOn,
                    scheduleMode);

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
        internal struct JobEventReaderForEachStructParallel<TJob, T>
            where TJob : struct, IJobEventReaderForEach<T>
            where T : unmanaged
        {
            /// <summary> The <see cref="NativeEventStream.Reader"/> . </summary>
            [ReadOnly]
            public NativeEventStream.Reader Reader;

            /// <summary> The job. </summary>
            public TJob JobData;

            /// <summary> The index of the reader. </summary>
            public int Index;

            // ReSharper disable once StaticMemberInGenericType
            private static IntPtr jobReflectionData;

            private delegate void ExecuteJobFunction(
                ref JobEventReaderForEachStructParallel<TJob, T> fullData,
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
#if UNITY_2020_2_OR_NEWER
                    jobReflectionData = JobsUtility.CreateJobReflectionData(
                        typeof(JobEventReaderForEachStructParallel<TJob, T>),
                        typeof(TJob),
                        (ExecuteJobFunction)Execute);
#else
                    jobReflectionData = JobsUtility.CreateJobReflectionData(
                        typeof(JobEventReaderForEachStructParallel<TJob, T>),
                        typeof(TJob),
                        JobType.ParallelFor,
                        (ExecuteJobFunction)Execute);
#endif
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
                ref JobEventReaderForEachStructParallel<TJob, T> fullData,
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
                        fullData.JobData.Execute(fullData.Reader, i);
                    }
                }
            }
        }
    }
}
