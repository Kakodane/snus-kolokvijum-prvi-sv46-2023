using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SNUS_kolokvijum_1
{
    internal class ProcessingSystem
    {
        private readonly PriorityQueue<(Job, TaskCompletionSource<int>),int> queue = new();
        private readonly object _lock = new();
        private readonly object _lockWrite= new();
        private readonly HashSet<Guid> processedIds = new();
        private readonly int maxQueueSize;
        private readonly SemaphoreSlim semaphore=new SemaphoreSlim(0);

        public event EventHandler<CustomEventArgs> jobCompleted;
        public event EventHandler<CustomEventArgs> jobFailed;

        public static List<Task> writeTasks= new List<Task>();

        private readonly Dictionary<Job,long> processedJobs= new Dictionary<Job,long>();

        public ProcessingSystem(int workerCount, int maxQueueSize)
        {
            this.maxQueueSize = maxQueueSize;
            jobCompleted += (s, e) =>
            {
                writeTasks.Add(Task.Run(()=>WriteInFile(s,e)));
            };
            jobFailed += (s, e) =>
            {
                writeTasks.Add(Task.Run(() => WriteInFile(s, e)));
            };
            for(int i = 0; i < workerCount; i++)
            {
                Task.Run(Consume);
            }

            Task.Run(async () => //moze i while true pa da se radi task.delay(60000)
            {
                using var timer = new PeriodicTimer(TimeSpan.FromMinutes(0.05));
                int reportCounter = 0;

                while (await timer.WaitForNextTickAsync())
                {
                    try
                    {
                        GenerateReport(++reportCounter);
                        if (reportCounter >= 10) reportCounter = 0;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");
                    }
                }
            });
        }
        public JobHandle Submit(Job job)
        { 
            lock (_lock)
            {
                if (processedIds.Contains(job.Id))
                {
                Console.WriteLine($"Job {job.Id} is already processed, skipping.");
                return null;
                }
                if (queue.Count >= maxQueueSize)
                {
                Console.WriteLine($"Queue is full, canceling {job.Id}.");
                return null;
                }
                TaskCompletionSource<int> tcs = new TaskCompletionSource<int>();
                queue.Enqueue((job, tcs),job.Priority);
                processedIds.Add(job.Id);
                semaphore.Release();
                return new JobHandle { Result = tcs.Task };
            }
        }

        private async Task Consume()
        {
            while (true)
            {
                await semaphore.WaitAsync();

                Job job;
                TaskCompletionSource<int> tcs;
                lock (_lock)
                {
                    if (queue.Count == 0) continue;
                    (job,tcs)=queue.Dequeue();
                }
                try
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    int result = await HandleJob(job);
                    sw.Stop();
                    
                    if(sw.ElapsedMilliseconds > 2000)
                    {

                        lock (_lock)
                        {
                            job.Retries++;
                            if (job.Retries >= 3)
                            {
                                jobFailed?.Invoke(this, new CustomEventArgs(job, result, Status.Abort));
                                tcs.SetResult(-1);
                                processedJobs.Add(job, sw.ElapsedMilliseconds);
                                Console.WriteLine($"Job {job.Id} aborted after 3 retries.");
                                continue;
                            }
                        }
                        jobFailed?.Invoke(this, new CustomEventArgs(job, result, Status.Fail));
                        lock (_lock) { 
                            Console.WriteLine($"Job {job.Id} failed, retrying ({job.Retries}/2).");
                            queue.Enqueue((job, tcs), job.Priority);
                            semaphore.Release();
                        }
                        await Task.Yield();
                    }
                    else
                    {
                        tcs.SetResult(result);
                        jobCompleted?.Invoke(this, new CustomEventArgs(job,result,Status.Success));
                        processedJobs.Add(job, sw.ElapsedMilliseconds);
                    }
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }
        }
        public static async Task<int> HandleJob(Job job)
        {
            if (job.Type == JobType.IO)
            {
                int delay = int.Parse(job.Payload.Split(':')[1].Replace("_", ""));
                Console.WriteLine($"Processing IO job with {delay} numbers.");
                //Thread.Sleep(delay);
                await Task.Delay(delay);
                Console.WriteLine($"Finished processing IO job with {delay} ms delay.");
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

        private void WriteInFile(object o, CustomEventArgs e)
        {
            lock (_lockWrite)
            {
                if (e.Status == Status.Abort)
                {
                    File.AppendAllText("job_handle_res.txt", $"[{DateTime.Now}] [{e.Status}] {e.Job.Id}\n");
                }
                else
                {
                    File.AppendAllText("job_handle_res.txt", $"[{DateTime.Now}] [{e.Status}] {e.Job.Id}, {e.Result}\n");
                }
            }
        }

        public IEnumerable<Job> GetTopJobs(int n)
        {
            lock (_lock)
            {
                return queue.UnorderedItems.OrderBy(x => x.Priority).Take(n).Select(x => x.Element.Item1).ToList();
            }
        }

        public Job GetJob(Guid id)
        {
            lock (_lock)
            {
                return queue.UnorderedItems.Select(x => x.Element.Item1).FirstOrDefault(j => j.Id == id);
            }
        }

        private void GenerateReport(int num)
        {
            lock (_lock)
            {
                var numOfJobsDonePerType = processedJobs.GroupBy(j => j.Key.Type).ToDictionary(g => g.Key,g=> g.Count());
                var avgTimePerType = processedJobs.GroupBy(j => j.Key.Type).ToDictionary(g => g.Key, g => g.Average(j => j.Value));
                var numOfAborts = processedJobs.Count(j => j.Key.Retries == 3);



                XElement report = new XElement($"Report", new XAttribute("GeneratedAt", DateTime.Now),
                    new XAttribute("ReportIndex", num),
                    numOfJobsDonePerType.Select(kv => new XElement(kv.Key.ToString(), new XAttribute("Count", kv.Value))),
                    avgTimePerType.Select(kv => new XElement(kv.Key.ToString(), new XAttribute("AverageTimeMilliseconds", kv.Value))),
                    new XElement("AbortedJobs", new XAttribute("Count", numOfAborts))
                    );


                report.Save($"report_{num}.xml");
            }
        }
    }
}
