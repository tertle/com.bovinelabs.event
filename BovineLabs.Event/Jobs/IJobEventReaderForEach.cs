// <copyright file="IJobEventReaderForEach.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Event.Jobs
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using BovineLabs.Core.Collections;
    using BovineLabs.Event.Containers;
    using BovineLabs.Event.Systems;
    using Unity.Collections;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Jobs;
    using Unity.Jobs.LowLevel.Unsafe;

    /// <summary> Job that visits each index in each event stream. </summary>
    [JobProducerType(typeof(JobEventReaderForEach.JobEventReaderForEachStructParallel<>))]
    [SuppressMessage("ReSharper", "TypeParameterCanBeVariant", Justification = "Strict requirements for compiler")]
    [SuppressMessage("ReSharper", "UnusedTypeParameter", Justification = "Required by scheduler")]
    public interface IJobEventReaderForEach
    {
        /// <summary> Executes the next event. </summary>
        /// <param name="stream"> The stream. </param>
        /// <param name="foreachIndex"> The foreach index. </param>
        void Execute(NativeEventStream.Reader stream, int foreachIndex);
    }

    /// <summary> Extension methods for <see cref="IJobEventReaderForEach"/> . </summary>
    public static class JobEventReaderForEach
    {
        /// <summary> Schedule a <see cref="IJobEventReaderForEach"/> job. </summary>
        /// <param name="jobData"> The job. </param>
        /// <param name="consumer"> The consumer. </param>
        /// <param name="dependsOn"> The job handle dependency. </param>
        /// <typeparam name="TJob"> The type of the job. </typeparam>
        /// <typeparam name="T"> The type of the key in the hash map. </typeparam>
        /// <returns> The handle to job. </returns>
        public static unsafe JobHandle ScheduleParallel<TJob, T>(
            this TJob jobData, EventConsumer<T> consumer, JobHandle dependsOn = default)
            where TJob : struct, IJobEventReaderForEach
            where T : unmanaged
        {
            if (!consumer.HasReaders)
            {
                return dependsOn;
            }

            dependsOn = consumer.GetReaders(dependsOn, out var readers);

            for (var i = 0; i < readers.Length; i++)
            {
                var reader = readers[i];

                var fullData = new JobEventReaderForEachStructParallel<TJob>
                {
                    Reader = readers[i],
                    JobData = jobData,
                    Index = i,
                };

                var scheduleParams = new JobsUtility.JobScheduleParameters(
                    UnsafeUtility.AddressOf(ref fullData),
                    JobEventReaderForEachStructParallel<TJob>.Initialize(),
                    dependsOn,
                    ScheduleMode.Parallel);

                dependsOn = JobsUtility.ScheduleParallelFor(
                    ref scheduleParams,
                    reader.ForEachCount,
                    1);
            }

            readers.Dispose(dependsOn);
            consumer.AddJobHandle(dependsOn);

            return dependsOn;
        }

        /// <summary> Schedule a <see cref="IJobEventReaderForEach"/> job. </summary>
        /// <param name="jobData"> The job. </param>
        /// <param name="readers"> The readers. </param>
        /// <param name="dependsOn"> The job handle dependency. </param>
        /// <typeparam name="TJob"> The type of the job. </typeparam>
        /// <returns> The handle to job. </returns>
        public static unsafe JobHandle ScheduleParallel<TJob>(
            this TJob jobData, UnsafeReadArray<NativeEventStream.Reader> readers, JobHandle dependsOn = default)
            where TJob : struct, IJobEventReaderForEach
        {
            for (var i = 0; i < readers.Length; i++)
            {
                var reader = readers[i];

                var fullData = new JobEventReaderForEachStructParallel<TJob>
                {
                    Reader = readers[i],
                    JobData = jobData,
                    Index = i,
                };

                var scheduleParams = new JobsUtility.JobScheduleParameters(
                    UnsafeUtility.AddressOf(ref fullData),
                    JobEventReaderForEachStructParallel<TJob>.Initialize(),
                    dependsOn,
                    ScheduleMode.Parallel);

                dependsOn = JobsUtility.ScheduleParallelFor(
                    ref scheduleParams,
                    reader.ForEachCount,
                    1);
            }

            readers.Dispose(dependsOn);

            return dependsOn;
        }

        /// <summary> The job execution struct. </summary>
        /// <typeparam name="TJob"> The type of the job. </typeparam>
        internal struct JobEventReaderForEachStructParallel<TJob>
            where TJob : struct, IJobEventReaderForEach
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
                ref JobEventReaderForEachStructParallel<TJob> fullData,
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
                        typeof(JobEventReaderForEachStructParallel<TJob>),
                        typeof(TJob),
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
                ref JobEventReaderForEachStructParallel<TJob> fullData,
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

                    for (var i = begin; i < end; i++)
                    {
                        fullData.JobData.Execute(fullData.Reader, i);
                    }
                }
            }
        }
    }
}
