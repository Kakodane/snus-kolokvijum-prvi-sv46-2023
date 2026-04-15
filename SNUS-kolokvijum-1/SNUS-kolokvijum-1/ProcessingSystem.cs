using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SNUS_kolokvijum_1
{
    internal class ProcessingSystem
    {
        private readonly PriorityQueue<Job, int> queue = new();
        private readonly object _lock = new();
        private readonly HashSet<Guid> processedIds = new();
        private readonly int maxQueueSize;
        private readonly SemaphoreSlim semaphore;

        public ProcessingSystem(int workerCount, int maxQueueSize)
        {
            this.maxQueueSize = maxQueueSize;
            semaphore = new SemaphoreSlim(workerCount);
        }
        public Task<JobHandle> Submit(Job job)
        {
            
            lock (_lock)
            {
                if (processedIds.Contains(job.Id))
                {
                Console.WriteLine($"Job {job.Id} vec obrađen, preskacem.");
                return null;
                }
                if (queue.Count >= maxQueueSize)
                {
                Console.WriteLine($"Queue je pun, odbacujem job {job.Id}.");
                return null;
                }
            
                queue.Enqueue(job, job.Priority);
                processedIds.Add(job.Id);
            }
            Task<int> processingTask = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    Job toProcess;
                    lock (_lock)
                    {
                        toProcess = queue.Dequeue();
                    }
                    return await HandleJob(toProcess);
                }
                finally
                {
                    semaphore.Release();
                }
            });
           
            JobHandle handle = new JobHandle { Id = job.Id, Result = processingTask };
            return Task.FromResult(handle);
        }
        public static async Task<int> HandleJob(Job job)
        {
            if (job.Type == JobType.IO)
            {
                int delay = int.Parse(job.Payload.Split(':')[1].Replace("_", ""));
                Console.WriteLine($"Processing IO job with {delay} numbers.");
                //Thread.Sleep(delay);
                await Task.Delay(delay);
                int randNum = Random.Shared.Next(0, 101);
                return randNum ;
            }
            else if (job.Type == JobType.Prime)
            {
                string[] parts = job.Payload.Split(',');

                int numbers = int.Parse(parts[0].Split(':')[1].Replace("_", ""));
                int threads = int.Parse(parts[1].Split(':')[1]);
                Console.WriteLine($"Processing Prime job with {numbers} numbers and {threads} threads.");
                int numberOfPrimes = 0;
                List<Task> tasks = new List<Task>();
                for (int i=0; i < threads; i++)
                {
                    int start = i * (numbers / threads) + 1;
                    int end = (i + 1) * (numbers / threads);
                   tasks.Add( Task.Run(() =>
                    {
                        for (int num = start; num <= end; num++)
                        {
                            if (isPrime(num))
                            {
                                Interlocked.Increment(ref numberOfPrimes);
                            }
                        }
                    }));
                  
                }

                await Task.WhenAll(tasks);
                Console.WriteLine($"Found {numberOfPrimes} prime numbers up to {numbers}.");
                return numberOfPrimes;
            }
            else
            {
                return -1;
            }
            
        }

        private static Boolean isPrime(int num)
        {
            if (num <= 1) return false;
            if (num == 2) return true;
            if (num % 2 == 0) return false;
            for (int i = 3; i <= Math.Sqrt(num); i += 2)
            {
                if (num % i == 0) return false;
            }
            return true;
        }
    }
}
