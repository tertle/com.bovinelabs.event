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
    [JobProducerType(typeof(JobEvent.JobEventProducer<,>))]
    [SuppressMessage("ReSharper", "TypeParameterCanBeVariant", Justification = "Strict requirements for compiler")]
    public interface IJobEvent<T>
        where T : unmanaged
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
        public static JobHandle Schedule<TJob, T>(
            this TJob jobData,
            EventSystemBase eventSystem,
            JobHandle dependsOn = default)
            where TJob : struct, IJobEvent<T>
            where T : unmanaged
        {
            return ScheduleInternal<TJob, T>(jobData, eventSystem, dependsOn, false);
        }

        /// <summary> Schedule a <see cref="IJobEvent{T}"/> job. </summary>
        /// <param name="jobData"> The job. </param>
        /// <param name="eventSystem"> The event system. </param>
        /// <param name="dependsOn"> T he job handle dependency. </param>
        /// <typeparam name="TJob"> The type of the job. </typeparam>
        /// <typeparam name="T"> The type of the key in the hash map. </typeparam>
        /// <returns> The handle to job. </returns>
        public static JobHandle ScheduleParallel<TJob, T>(
            this TJob jobData,
            EventSystemBase eventSystem,
            JobHandle dependsOn = default)
            where TJob : struct, IJobEvent<T>
            where T : unmanaged
        {
            return ScheduleInternal<TJob, T>(jobData, eventSystem, dependsOn, true);
        }

        private static unsafe JobHandle ScheduleInternal<TJob, T>(
            this TJob jobData,
            EventSystemBase eventSystem,
            JobHandle dependsOn,
            bool isParallel)
            where TJob : struct, IJobEvent<T>
            where T : unmanaged
        {
            dependsOn = eventSystem.GetEventReaders<T>(dependsOn, out var events);

            for (var i = 0; i < events.Count; i++)
            {
                var reader = events[i];

                var fullData = new JobEventProducer<TJob, T>
                {
                    Reader = reader,
                    JobData = jobData,
                    IsParallel = isParallel,
                };

#if UNITY_2020_2_OR_NEWER
                const ScheduleMode scheduleMode = ScheduleMode.Parallel;
#else
                const ScheduleMode scheduleMode = ScheduleMode.Batched;
#endif

                var scheduleParams = new JobsUtility.JobScheduleParameters(
                    UnsafeUtility.AddressOf(ref fullData),
                    isParallel ? JobEventProducer<TJob, T>.InitializeParallel() : JobEventProducer<TJob, T>.InitializeSingle(),
                    dependsOn,
                    scheduleMode);

                dependsOn = isParallel
                    ? JobsUtility.ScheduleParallelFor(ref scheduleParams, reader.ForEachCount, 1)
                    : JobsUtility.Schedule(ref scheduleParams);
            }

            eventSystem.AddJobHandleForConsumer<T>(dependsOn);

            return dependsOn;
        }

        /// <summary> The parallel job execution struct. </summary>
        /// <typeparam name="TJob"> The type of the job. </typeparam>
        /// <typeparam name="T"> The type of the event. </typeparam>
        internal struct JobEventProducer<TJob, T>
            where TJob : struct, IJobEvent<T>
            where T : unmanaged
        {
            /// <summary> The <see cref="NativeEventStream.Reader"/> . </summary>
            [ReadOnly]
            public NativeEventStream.Reader Reader;

            /// <summary> The job. </summary>
            public TJob JobData;

            public bool IsParallel;

            // ReSharper disable once StaticMemberInGenericType
            private static IntPtr jobReflectionDataSingle;

            // ReSharper disable once StaticMemberInGenericType
            private static IntPtr jobReflectionDataParallel;

            private delegate void ExecuteJobFunction(
                ref JobEventProducer<TJob, T> fullData,
                IntPtr additionalPtr,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex);

            /// <summary> Initializes the job. </summary>
            /// <returns> The job pointer. </returns>
            public static IntPtr InitializeSingle()
            {
                if (jobReflectionDataSingle == IntPtr.Zero)
                {
#if UNITY_2020_2_OR_NEWER
                    jobReflectionDataSingle = JobsUtility.CreateJobReflectionData(
                        typeof(JobEventProducer<TJob, T>),
                        typeof(TJob),
                        (ExecuteJobFunction)Execute);
#else
                    jobReflectionDataSingle = JobsUtility.CreateJobReflectionData(
                        typeof(JobEventProducer<TJob, T>),
                        typeof(TJob),
                        JobType.Single,
                        (ExecuteJobFunction)Execute);
#endif
                }

                return jobReflectionDataSingle;
            }

            /// <summary> Initializes the job. </summary>
            /// <returns> The job pointer. </returns>
            public static IntPtr InitializeParallel()
            {
                if (jobReflectionDataParallel == IntPtr.Zero)
                {
#if UNITY_2020_2_OR_NEWER
                    jobReflectionDataParallel = JobsUtility.CreateJobReflectionData(
                        typeof(JobEventProducer<TJob, T>),
                        typeof(TJob),
                        (ExecuteJobFunction)Execute);
#else
                    jobReflectionDataParallel = JobsUtility.CreateJobReflectionData(
                        typeof(JobEventProducer<TJob, T>),
                        typeof(TJob),
                        JobType.ParallelFor,
                        (ExecuteJobFunction)Execute);
#endif
                }

                return jobReflectionDataParallel;
            }

            /// <summary> Executes the job. </summary>
            /// <param name="fullData"> The job data. </param>
            /// <param name="additionalPtr"> AdditionalPtr. </param>
            /// <param name="bufferRangePatchData"> BufferRangePatchData. </param>
            /// <param name="ranges"> The job range. </param>
            /// <param name="jobIndex"> The job index. </param>
            [SuppressMessage("ReSharper", "MemberCanBePrivate.Global", Justification = "Required by burst.")]
            public static void Execute(
                ref JobEventProducer<TJob, T> fullData,
                IntPtr additionalPtr,
                IntPtr bufferRangePatchData,
                ref JobRanges ranges,
                int jobIndex)
            {
                while (true)
                {
                    int begin = 0;
                    int end = fullData.Reader.ForEachCount;

                    // If we are running the job in parallel, steal some work.
                    if (fullData.IsParallel)
                    {
                        if (!JobsUtility.GetWorkStealingRange(ref ranges, jobIndex, out begin, out end))
                        {
                            return;
                        }
                    }

                    for (int i = begin; i < end; i++)
                    {
                        var count = fullData.Reader.BeginForEachIndex(i);

                        for (var j = 0; j < count; j++)
                        {
                            var e = fullData.Reader.Read<T>();
                            fullData.JobData.Execute(e);
                        }

                        fullData.Reader.EndForEachIndex();
                    }

                    if (!fullData.IsParallel)
                    {
                        break;
                    }
                }
            }
        }
    }
}
