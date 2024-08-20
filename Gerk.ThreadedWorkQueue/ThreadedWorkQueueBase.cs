using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Gerk.ThreadedWorkQueue
{
	/// <summary>
	/// Abstract base class that both <see cref="ThreadedWorkQueue{T}"/> and <see cref="ThreadedWorkQueue{T, PassAhead}"/> inherit from. Supports <see cref="EnqueueWork(T)"/> and <see cref="EmptyQueue"/>.
	/// To create one please inherit from <see cref="ThreadedWorkQueue{T}"/> or <see cref="ThreadedWorkQueue{T, PassAhead}"/>.
	/// </summary>
	/// <seealso cref="ThreadedWorkQueue{T}"/>
	/// <seealso cref="ThreadedWorkQueue{T, PassAhead}"/>
	/// <typeparam name="T">A type to be put into the queue and consumed.</typeparam>
	public abstract class ThreadedWorkQueueBase<T>
	{
		// Used by EmptyQueue
		internal TaskCompletionSource<bool> completionEvent;

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

		internal readonly ConcurrentQueue<T> internalQueue = new ConcurrentQueue<T>();

		internal bool threadExpired = true;
		internal readonly object threadExpiredLock = new object();

		internal bool EndThreadIfNeeded()
		{
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
				}
			}
			return threadExpired;
		}

		internal abstract void ThreadFunc();

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
				new Thread(ThreadFunc).Start();
			}
		}
	}
}
