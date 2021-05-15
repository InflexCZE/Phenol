using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

public class AsyncCallChainExecutor : IDisposable
{
    public const int DEFAULT_DEPTH_ALLOCATION = 5;

    public bool IsComplete => this.TaskStack.Count == 0;
    public bool RethrowWithChainTrace { get; private set; }
    private readonly Stack<IEnumerator> TaskStack = new Stack<IEnumerator>(DEFAULT_DEPTH_ALLOCATION);

    public AsyncCallChainExecutor(IEnumerator rootTask, bool rethrowWithChainTrace = true)
    {
        this.TaskStack.Push(rootTask);
        this.RethrowWithChainTrace = rethrowWithChainTrace;
    }

    /// <summary>
    /// Progress coroutine by one step
    /// </summary>
    /// <returns>True if all work is done, false otherwise</returns>
    public bool OnTick()
    {
        while(this.TaskStack.Count != 0)
        {
            var currentTask = this.TaskStack.Peek();

            try
            {
                var complete = currentTask.MoveNext() == false;

                if(complete)
                {
                    var releasedTask = this.TaskStack.Pop();
                    (releasedTask as IDisposable)?.Dispose();
                    continue;
                }
            }
            catch(Exception e) when(this.RethrowWithChainTrace)
            {
                var sb = new StringBuilder();
                sb.AppendLine("Exception thrown during call chain execution!");
                sb.AppendLine("ChainTrace:");

                foreach(var x in this.TaskStack)
                {
                    sb.Append("   at ");
                    sb.Append(PrintableName(x));
                    sb.AppendLine();
                }

                sb.AppendLine("   --- End of inner exception chain trace ---");
                sb.AppendLine(e.ToString());
                sb.AppendLine("   --- End of exception record ---");

                throw new Exception(sb.ToString(), e);
            }

            var newTask = currentTask.Current as IEnumerator;
            if(newTask != null)
            {
                this.TaskStack.Push(newTask);
                continue;
            }

            return false;
        }

        return true;
    }

    private static string PrintableName(IEnumerator enumerator)
    {
        var taskName = enumerator.GetType().Name;
        var endIndex = taskName.IndexOf('>');

        if(endIndex < 0)
            return taskName;

        return taskName.Substring(1, endIndex - 1);
    }

    public void Dispose()
    {
        while(this.TaskStack.Count > 0)
        {
            (this.TaskStack.Pop() as IDisposable)?.Dispose();
        }
    }
}
