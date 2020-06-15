using Humanizer;
using Humanizer.Localisation;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PanoramicSystems
{
	public abstract class LoopInterval
	{
		private readonly TimeSpan _timeSpanInterval;

		public ILogger Logger { get; }

		protected LoopInterval(string name, TimeSpan timeSpanInterval, ILogger logger)
		{
			_timeSpanInterval = timeSpanInterval;
			Logger = new PrefixLogger(name, logger);
		}

		public abstract Task ExecuteAsync(CancellationToken cancellationToken);

		/// <summary>
		/// Loops attempting to keep a minimum interval between the start of each execution.
		/// Exits when complete or cancelled.
		/// </summary>
		/// <param name="cancellationToken">CancellationToken</param>
		public async Task LoopAsync(CancellationToken cancellationToken)
		{
			// Create a Stopwatch to monitor how long the sync takes
			var stopwatch = Stopwatch.StartNew();

			while (!cancellationToken.IsCancellationRequested)
			{
				stopwatch.Restart();

				Logger.LogInformation("Starting...");

				try
				{
					await ExecuteAsync(cancellationToken).ConfigureAwait(false);
				}
				catch (Exception ex) when (ex is OperationCanceledException || ex is TaskCanceledException)
				{
					Logger.LogInformation(ex, "Cancelled during execution.");
				}
				catch (Exception ex)
				{
					Logger.LogError(ex, $"An unexpected error occurred during the LoopInterval execution: {ex.Message}");
				}
				// We always continue here so we can continue if an Exception occurred during execution that was not related to cancellation

				stopwatch.Stop();
				Logger.LogInformation($"Finished in {stopwatch.Elapsed.Humanize(7, minUnit: TimeUnit.Second)}.");

				if (cancellationToken.IsCancellationRequested)
				{
					// Return gracefully rather than throw an exeception
					return;
				}

				// YES - determine the interval
				var remainingTimeInInterval = _timeSpanInterval.Subtract(stopwatch.Elapsed);
				if (remainingTimeInInterval.TotalSeconds > 0)
				{
					Logger.LogInformation($"Next will start in {remainingTimeInInterval.Humanize(7, minUnit: TimeUnit.Second)} at {DateTime.UtcNow.Add(remainingTimeInInterval)}.");
					try
					{
						await Task.Delay(remainingTimeInInterval, cancellationToken).ConfigureAwait(false);
					}
					catch (TaskCanceledException ex)
					{
						Logger.LogInformation(ex, "Cancelled during interval delay.");
					}
				}
				else
				{
					Logger.LogWarning($"Next execution will start immediately as it took {stopwatch.Elapsed}, which is longer than the configured TimeSpan {_timeSpanInterval}.");
				}
			}
		}
	}
}
