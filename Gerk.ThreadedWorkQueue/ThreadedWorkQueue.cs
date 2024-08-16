using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Gerk.ThreadedWorkQueue
{
	/// <summary>
	/// Can be passed elements of <typeparamref name="T"/> and will use a single thread to execute <see cref="DigestItem(T)"/> function on each item. The thread will only be spun up when work is enqueued and will exist until all work is complete at which point it will exit. Sometimes like in the case of writing to a file a "flush" might be useful so the <see cref="FlushBuffer"/> can be overriden to have any logic that should happen infrequently when a batch of items are done being procesesed.
	/// </summary>
	/// <typeparam name="T">A type to be put into the queue and consumed.</typeparam>
	public abstract class ThreadedWorkQueue<T>
	{
		/// <summary>
		/// <see langword="override"/> to define how each item should be handled.
		/// </summary>
		/// <param name="item">The <typeparamref name="T"/> to be worked on.</param>
		abstract protected void DigestItem(T item);
		/// <summary>
		/// Defaults to nothing. <see langword="override"/> to define logic that is run infrequently when the queue empties.
		/// </summary>
		virtual protected void FlushBuffer() { }


		// Used by EmptyQueue
		private TaskCompletionSource<bool> completionEvent;

		/// <summary>
		/// Used to wait for the queue to complete its work.
		/// </summary>
		/// <remarks>May be useful for debugging.</remarks>
		/// <returns>A task that yields when the queue is empty.</returns>
		public Task EmptyQueue()
		{
			lock (threadExpiredLock)
				if (threadExpired)
					return Task.FromResult(true);
				else if (completionEvent == null)
					completionEvent = new TaskCompletionSource<bool>();

			return completionEvent.Task;
		}

		private readonly ConcurrentQueue<T> internalQueue = new ConcurrentQueue<T>();

		private bool threadExpired = true;
		private readonly object threadExpiredLock = new object();

		private void LoggerThreadFunc()
		{
			for (; ; )
			{
				do
				{
					if (internalQueue.TryDequeue(out T item))
					{
						DigestItem(item);
					}
				} while (!internalQueue.IsEmpty);

				FlushBuffer();

				lock (threadExpiredLock)
				{
					threadExpired = internalQueue.IsEmpty;
					if (threadExpired)
					{
						if (completionEvent != null)
						{
							completionEvent.TrySetResult(true);
							completionEvent = null;
						}
						break;
					}
				}
			}
		}

		/// <summary>
		/// Add an item to be handled. Will spin up the thread if one isn't currently running.
		/// </summary>
		/// <param name="item">The data for the item.</param>
		public void EnqueueWork(T item)
		{
			bool createNewThread;
			internalQueue.Enqueue(item);
			lock (threadExpiredLock)
			{
				createNewThread = threadExpired;
				threadExpired = false;
			}
			if (createNewThread)
			{
				new Thread(LoggerThreadFunc).Start();
			}
		}
	}
}
