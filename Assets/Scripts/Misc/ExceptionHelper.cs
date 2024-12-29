using System;
using System.Text;

public static class ExceptionHelper
{
    /// <summary>
    /// Returns a detailed string representation of the given exception,
    /// including all inner exceptions, their messages, and stack traces.
    /// </summary>
    /// <param name="exception">The exception to detail.</param>
    /// <returns>A formatted string with exception details.</returns>
    public static string GetFullExceptionDetails(Exception exception)
    {
        if (exception == null)
        {
            return "No exception provided.";
        }

        var sb = new StringBuilder();
        int level = 0;
        Exception currentException = exception;

        // Walk through all levels of inner exceptions
        while (currentException != null)
        {
            sb.AppendLine($"--- Exception Level {level} ---");
            sb.AppendLine($"Type:       {currentException.GetType().FullName}");
            sb.AppendLine($"Message:    {currentException.Message}");
            sb.AppendLine("Stack Trace:");
            sb.AppendLine(currentException.StackTrace ?? "No stack trace available");
            sb.AppendLine();

            currentException = currentException.InnerException;
            level++;
        }

        return sb.ToString();
    }
}