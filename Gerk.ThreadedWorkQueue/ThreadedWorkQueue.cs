using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Gerk.ThreadedWorkQueue
{
	/// <summary>
	/// Can be passed elements of <typeparamref name="T"/> and will use a single thread to execute <see cref="DigestItem(T)"/> function on each item. The thread will only be spun up when work is enqueued and will exist until all work is complete at which point it will exit. Sometimes like in the case of writing to a file a "flush" might be useful so the <see cref="FlushBuffer"/> can be overridden to have any logic that should happen infrequently when a batch of items are done being processed.
	/// </summary>
	/// <typeparam name="T">A type to be put into the queue and consumed.</typeparam>
	public abstract class ThreadedWorkQueue<T> : ThreadedWorkQueueBase<T>
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

		internal override void ThreadFunc()
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

				if (EndThreadIfNeeded())
					break;
			}
		}
	}

	/// <summary>
	/// Can be passed elements of <typeparamref name="T"/> and will use a single thread to execute <see cref="DigestItem(T, ref PassAhead)"/> function on each item. The thread will only be spun up when work is enqueued and will exist until all work is complete at which point it will exit. Sometimes like in the case of writing to a file a "flush" might be useful so the <see cref="FlushBuffer"/> can be overridden to have any logic that should happen infrequently when a batch of items are done being processed.
	/// </summary>
	/// <typeparam name="T">A type to be put into the queue and consumed.</typeparam>
	/// <typeparam name="PassAhead">A type to hold information that is passed between calls of <see cref="DigestItem(T, ref PassAhead)"/> and <see cref="FlushBuffer(PassAhead)"/>. The first call of <see cref="DigestItem(T, ref PassAhead)"/> after a <see cref="FlushBuffer(PassAhead)"/> will always be <see langword="default"/>.</typeparam>
	public abstract class ThreadedWorkQueue<T, PassAhead> : ThreadedWorkQueueBase<T>
	{
		/// <summary>
		/// <see langword="override"/> to define how each item should be handled.
		/// </summary>
		/// <param name="item">The <typeparamref name="T"/> to be worked on.</param>
		/// <param name="passAlong">A <see langword="ref"/> to the <typeparamref name="PassAhead"/> being passed along. It will be reset to <see langword="default"/> after each call of <see cref="FlushBuffer(PassAhead)"/></param>
		abstract protected void DigestItem(T item, ref PassAhead passAlong);
		/// <summary>
		/// Defaults to nothing. <see langword="override"/> to define logic that is run infrequently when the queue empties.
		/// </summary>
		/// <param name="passAhead">The <typeparamref name="PassAhead"/> that has been being passed along through <see cref="DigestItem(T, ref PassAhead)"/> since the last call of <see cref="FlushBuffer(PassAhead)"/>.</param>
		virtual protected Task FlushBuffer(PassAhead passAhead) { return Task.FromResult(true); }
		/// <summary>
		/// Defaults to returning <see langword="default"/>. Override to define how the <typeparamref name="PassAhead"/> is going to be initialized when needed.
		/// </summary>
		/// <returns>The <typeparamref name="PassAhead"/> object.</returns>
		virtual protected PassAhead InitializePassAhead() { return default; }

		internal override void ThreadFunc()
		{
			for (; ; )
			{
				PassAhead passAlong = InitializePassAhead();
				do
				{
					if (internalQueue.TryDequeue(out T item))
					{
						DigestItem(item, ref passAlong);
					}
				} while (!internalQueue.IsEmpty);
				FlushBuffer(passAlong).Wait();
				if (EndThreadIfNeeded())
					break;
			}
		}
	}
}
