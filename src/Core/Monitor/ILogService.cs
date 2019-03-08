using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Gibraltar.Data;
using Gibraltar.Messaging;
using Loupe.Configuration;
using Loupe.Extensibility.Data;

namespace Gibraltar.Monitor
{
    public interface ILogService
    {
        /// <summary>
        /// Indicates if the logging system should be running in silent mode (for example when running in the agent).
        /// </summary>
        /// <remarks>Pass-through to the setting in CommonFileTools.</remarks>
        bool SilentMode { get; set; }

        /// <summary>
        /// Indicates if the process is running under the Mono runtime or the full .NET CLR.
        /// </summary>
        bool IsMonoRuntime { get; }

        /// <summary>
        /// The running publisher configuration.  This is always safe even when logging is disabled.
        /// </summary>
        AgentConfiguration Configuration { get; }

        /// <summary>
        /// The common information about the active log session.  This is always safe even when logging is disabled.
        /// </summary>
        SessionSummary SessionSummary { get; }

        /// <summary>
        /// Get the official Error Alert Notifier instance.  Will create it if it doesn't already exist.
        /// </summary>
        Notifier MessageAlertNotifier { get; }

        /// <summary>
        /// Get the official Notifier instance that returns all messages.  Will create it if it doesn't already exist.
        /// </summary>
        Notifier MessageNotifier { get; }

        /// <summary>
        /// Get the official user resolution notifier instance.  Will create it if it doesn't already exist.
        /// </summary>
        UserResolutionNotifier UserResolutionNotifier { get; }

        /// <summary>
        /// The current process's collection repository
        /// </summary>
        LocalRepository Repository { get; }

        /// <summary>
        /// Indicates if the agent should package &amp; send sessions for the current application after this session exits.
        /// </summary>
        /// <remarks>When true the system will automatically </remarks>
        bool SendSessionsOnExit
        {
            get;
            [MethodImpl(MethodImplOptions.NoInlining)] // Does this work here? Is it needed?
            set;
        }

        /// <summary>
        /// Indicates if the StartSession API method was ever explicitly called.
        /// </summary>
        /// <remarks>If StartSession was not explicitly called then an ApplicationExit event will implicitly call
        /// EndSession for easy Gibraltar drop-in support.  If StartSession was explicitly called then we expect
        /// the client to make a corresponding explicit EndSession call, and the Agent's ApplicationExit handler
        /// will not call EndSession.</remarks>
        bool ExplicitStartSessionCalled { get; }

        /// <summary>
        /// Our one metric definition collection for capturing metrics in this process
        /// </summary>
        /// <remarks>
        /// For performance reasons, it is important that there is only a single instance of a particular metric
        /// for any given process.  This is managed automatically provided only this metrics collection is used.
        /// If there is a duplicate metric in the data stream, that information will be discarded when the log 
        /// file is read (but there is no effect at runtime).
        /// </remarks>
        MetricDefinitionCollection Metrics { get; }

        /// <summary>
        /// The version information for the Gibraltar Agent.
        /// </summary>
        Version AgentVersion { get; }

        /// <summary>
        /// A temporary flag to tell us whether to invoke a Debugger.Break() when Log.DebugBreak() is called.
        /// </summary>
        /// <remarks>True enables breakpointing, false disables.  This should probably be replaced with an enum
        /// to support multiple modes, assuming the basic usage works out.</remarks>
        bool BreakPointEnable { get; set; }

        /// <summary>
        /// Indicates if the log system has been initialized and is operational
        /// </summary>
        /// <remarks>Once true it will never go false, however if false it may go true at any time.</remarks>
        bool Initialized { get; }

        /// <summary>
        /// Reports whether EndSession() has been called to formally end the session.
        /// </summary>
        bool IsSessionEnding { get; }

        /// <summary>
        /// Reports whether EndSession() has completed flushing the end-session command to the log.
        /// </summary>
        bool IsSessionEnded { get; }

        /// <summary>
        /// Indicates if the calling thread is part of the log initialization process
        /// </summary>
        bool ThreadIsInitializer { get; set; }

        /// <summary>
        /// Indicates if logging is active, performing initialization if necessary
        /// </summary>
        /// <returns>True if logging is active, false if it isn't at this time.</returns>
        /// <remarks>The very first time this is used it will attempt to start the logging system even if 
        /// it hasn't already been started.  If that call is canceled through our Initializing event then 
        /// it will return false.  After the first call it will indicate if logging is currently initialized
        /// and not attempt to initialize.</remarks>
        bool IsLoggingActive();

        /// <summary>
        /// Attempt to initialize the log system.  If it is already initialized it will return immediately.
        /// </summary>
        /// <param name="configuration">Optional.  A default configuration to start with instead of the configuration file.</param>
        /// <returns>True if the initialization has completed (on this call or prior),
        /// false if a re-entrant call returns to avoid deadlocks and infinite recursion.</returns>
        /// <remarks>If calling initialization from a path that may have started with the trace listener,
        /// you must set suppressTraceInitialize to true to guarantee that the application will not deadlock
        /// or throw an unexpected exception.</remarks>
        bool Initialize(AgentConfiguration configuration);

        /// <summary>
        /// Indicates if we have sufficient configuration information to automatically send packages while running (via email or server).
        /// </summary>
        /// <remarks>This checks whether there is sufficient configuration to submit sessions using the current configuration.</remarks>
        /// <returns></returns>
        bool CanSendSessions(ref string message);

        /// <summary>
        /// Indicates if we have sufficient configuration information to automatically send packages upon exit (via email or server).
        /// </summary>
        /// <remarks>This checks whether there is sufficient configuration to submit sessions through the packager upon exit.
        /// It also checks that the packager executable can be found.</remarks>
        /// <returns></returns>
        bool CanSendSessionsOnExit(ref string message);

        /// <summary>
        /// Ensure all messages have been written completely
        /// </summary>
        void Flush();

        /// <summary>
        /// Indicates if we have sufficient configuration information to automatically send packages by email submission.
        /// </summary>
        /// <returns></returns>
        /// <remarks>Does not check if email submission is allowed</remarks>
        bool IsEmailSubmissionConfigured(ref string message);

        /// <summary>
        /// Indicates if we have sufficient configuration information to automatically send packages to a Loupe Server.
        /// </summary>
        /// <remarks>This checks whether there is sufficient configuration to submit sessions through a server.
        /// It does NOT check whether the packager is configured to allow submission through a server, because
        /// they may also be sent directly from Agent without using the packager.</remarks>
        /// <returns></returns>
        bool IsHubSubmissionConfigured(ref string message);

        /// <summary>
        /// Indicates if the packager executable is available where this process can find it.
        /// </summary>
        bool CanFindPackager(ref string message);

        /// <summary>
        /// Record the provided set of metric samples to the log.
        /// </summary>
        /// <remarks>When sampling multiple metrics at the same time, it is faster to make a single write call
        /// than multiple calls.</remarks>
        /// <param name="samples">A list of metric samples to record.</param>
        void Write(List<MetricSample> samples);

        /// <summary>
        /// Record the provided metric sample to the log.
        /// </summary>
        /// <remarks>Most applications should use another object or the appropriate log method on this object to
        /// create log information instead of manually creating log packets and writing them here.  This functionality
        /// is primarily for internal support of the various log listeners that support third party log systems.</remarks>
        /// <param name="sample"></param>
        void Write(MetricSample sample);

        /// <summary>
        /// Write a trace message directly to the Gibraltar log.
        /// </summary>
        /// <remarks>The log message will be attributed to the caller of this method.  Wrapper methods should
        /// instead call the WriteMessage() method in order to attribute the log message to their own outer
        /// callers.</remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="category">The category for this log message.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        void Write(LogMessageSeverity severity, string category, string caption, string description, params object[] args);

        /// <summary>
        /// Write a log message directly to the Gibraltar log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>The log message will be attributed to the caller of this method.  Wrapper methods should
        /// instead call the WriteMessage() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="category">The category for this log message.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        void Write(LogMessageSeverity severity, LogWriteMode writeMode, Exception exception, string category, string caption,
            string description, params object[] args);

        /// <summary>
        /// Write a log message directly to the Gibraltar log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>The log message will be attributed to the caller of this method.  Wrapper methods should
        /// instead call the WriteMessage() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="skipFrames">The number of stack frames to skip back over to determine the original caller.</param>
        /// <param name="category">The category for this log message.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        void Write(LogMessageSeverity severity, LogWriteMode writeMode, Exception exception, int skipFrames, string category, string caption,
            string description, params object[] args);

        /// <summary>
        /// Write a log message directly to the Gibraltar log with an attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>The log message will be attributed to the caller of this method.  Wrapper methods should
        /// instead call the WriteMessage() method in order to attribute the log message to their own outer callers.</para>
        /// <para>This overload also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True if the call stack from where the exception was thrown should be used for log message attribution</param>
        /// <param name="category">The category for this log message.</param>
        /// <param name="caption">A simple single-line message caption. (Will not be processed for formatting.)</param>
        /// <param name="description">Additional multi-line descriptive message (or may be null) which can be a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments referenced by the formatted description string (or no arguments to skip formatting).</param>
        void Write(LogMessageSeverity severity, LogWriteMode writeMode, Exception exception, bool attributeToException, string category, string caption,
            string description, params object[] args);

        /// <summary>
        /// Write a trace message directly to the Gibraltar log with an optional attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This overload of WriteMessage() is provided as an API hook for simple wrapping methods
        /// which need to attribute a log message to their own outer callers.  Passing a skipFrames of 0 would
        /// designate the caller of this method as the originator; a skipFrames of 1 would designate the caller
        /// of the caller of this method as the originator, and so on.  It will then extract information about
        /// the originator automatically based on the indicated stack frame.  Bridge logic adapting from a logging
        /// system which already determines and provides information about the originator (such as log4net) into
        /// Gibraltar should use the other overload of WriteMessage(), passing a customized IMessageSourceProvider.</para>
        /// <para>This method also requires explicitly selecting the LogWriteMode between Queued (the normal default,
        /// for optimal performance) and WaitForCommit (to help ensure critical information makes it to disk, e.g. before
        /// exiting the application upon return from this call).  See the LogWriteMode enum for more information.</para>
        /// <para>This method also allows an optional Exception object to be attached to the log message (null for
        /// none).  And the message may be a simple message string, or a format string followed by arguments.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="skipFrames">The number of stack frames to skip back over to determine the original caller.</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multi-line description to use which can be a format string for the arguments.  Can be null.</param>
        /// <param name="args">Optional.  A variable number of arguments to insert into the formatted description string.</param>
        void WriteMessage(LogMessageSeverity severity, LogWriteMode writeMode, int skipFrames, Exception exception,
            string detailsXml, string caption, string description, params object[] args);

        /// <summary>
        /// Write a trace message directly to the Gibraltar log with an optional attached Exception and specifying
        /// Queued or WaitForCommit behavior.
        /// </summary>
        /// <remarks><para>This overload of WriteMessage() is provided as an API hook for simple wrapping methods
        /// which need to attribute a log message to their own outer callers.  Passing a skipFrames of 0 would
        /// designate the caller of this method as the originator; a skipFrames of 1 would designate the caller
        /// of the caller of this method as the originator, and so on.  It will then extract information about
        /// the originator automatically based on the indicated stack frame.  Bridge logic adapting from a logging
        /// system which already determines and provides information about the originator (such as log4net) into
        /// Gibraltar should use the other overload of WriteMessage(), passing a customized IMessageSourceProvider.</para>
        /// <para>This method also requires explicitly selecting the LogWriteMode between Queued (the normal default,
        /// for optimal performance) and WaitForCommit (to help ensure critical information makes it to disk, e.g. before
        /// exiting the application upon return from this call).  See the LogWriteMode enum for more information.</para>
        /// <para>This method also allows an optional Exception object to be attached to the log message (null for
        /// none).  And the message may be a simple message string, or a format string followed by arguments.</para></remarks>
        /// <param name="severity">The log message severity.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        /// <param name="skipFrames">The number of stack frames to skip back over to determine the original caller.</param>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="attributeToException">True if the call stack from where the exception was thrown should be used for log message attribution</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multi-line description to use which can be a format string for the arguments.  Can be null.</param>
        /// <param name="args">Optional.  A variable number of arguments to insert into the formatted description string.</param>
        void WriteMessage(LogMessageSeverity severity, LogWriteMode writeMode, int skipFrames, Exception exception, bool attributeToException,
            string detailsXml, string caption, string description, params object[] args);

        /// <summary>
        /// Write a Verbose trace message directly to the Gibraltar log.
        /// </summary>
        /// <remarks>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations
        /// should instead use the Log.Write(...) overloads.</remarks>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        void Trace(string format, params object[] args);

        /// <summary>
        /// Write a Verbose trace message directly to the Gibraltar log.
        /// </summary>
        /// <remarks><para>Information about the current thread and calling method is automatically captured.
        /// The log message will be attributed to the immediate caller of this method.  Wrapper implementations
        /// should instead use the Log.Write(...) overloads.</para>
        /// <para>This method also allows an Exception object to be attached to the log message.  An Exception-typed
        /// null (e.g. from a variable of an Exception type) is allowed for the exception argument, but calls which
        /// do not have a possible Exception to attach should use an overload without an exception argument rather
        /// than pass a direct value of null, to avoid compiler ambiguity over the type of a simple null.</para></remarks>
        /// <param name="exception">An Exception object to attach to this log message.</param>
        /// <param name="format">The string message to use, or a format string followed by corresponding args.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted message string.</param>
        void Trace(Exception exception, string format, params object[] args);

        /// <summary>
        /// Record an unexpected Exception to the Gibraltar central log, formatted automatically.
        /// </summary>
        /// <remarks><para>This method provides an easy way to record an Exception as a separate message which will be
        /// attributed to the code location which threw the Exception rather than where this method was called from.
        /// The category will default to "Exception" if null, and the message will be formatted automatically based on the
        /// Exception.  The severity will be determined by the canContinue parameter:  Critical for fatal errors (canContinue
        /// is false), Error for non-fatal errors (canContinue is true).</para>
        /// <para>This method is intended for use with top-level exception catching for errors not anticipated in a
        /// specific operation, but when it is not appropriate to alert the user because the error does not impact their
        /// work flow or will be otherwise handled gracefully within the application.  For unanticipated errors which
        /// disrupt a user activity, see the <see CREF="ReportException">ReportException</see> method.</para></remarks>
        /// <param name="skipFrames">The number of stack frames to skip back over to determine the original caller.</param>
        /// <param name="exception">An Exception object to record as a log message.  This call is ignored if null.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the exception.  Can be null.</param>
        /// <param name="category">The application subsystem or logging category that the message will be associated with.</param>
        /// <param name="canContinue">True if the application can continue after this call, false if this is a fatal error
        /// and the application can not continue after this call.</param>
        void RecordException(int skipFrames, Exception exception, string detailsXml, string category,
            bool canContinue);

        /// <summary>
        /// Record an unexpected Exception to the Gibraltar central log, formatted automatically.
        /// </summary>
        /// <remarks><para>This method provides an easy way to record an Exception as a separate message which will be
        /// attributed to the code location which threw the Exception rather than where this method was called from.
        /// The category will default to "Exception" if null, and the message will be formatted automatically based on the
        /// Exception.  The severity will be determined by the canContinue parameter:  Critical for fatal errors (canContinue
        /// is false), Error for non-fatal errors (canContinue is true).</para>
        /// <para>This method is intended for use with top-level exception catching for errors not anticipated in a
        /// specific operation, but when it is not appropriate to alert the user because the error does not impact their
        /// work flow or will be otherwise handled gracefully within the application.  For unanticipated errors which
        /// disrupt a user activity, see the <see CREF="ReportException">ReportException</see> method.</para></remarks>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message (NOT the exception source information).</param>
        /// <param name="exception">An Exception object to record as a log message.  This call is ignored if null.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the exception.  Can be null.</param>
        /// <param name="category">The application subsystem or logging category that the message will be associated with.</param>
        /// <param name="canContinue">True if the application can continue after this call, false if this is a fatal error
        /// and the application can not continue after this call.</param>
        void RecordException(IMessageSourceProvider sourceProvider, Exception exception, string detailsXml,
            string category, bool canContinue);

        /// <summary>
        /// Write a complete log message to the Gibraltar central log.
        /// </summary>
        /// <remarks>Used as an API entry point for interfaces for other logging systems to hand off log messages
        /// into Gibraltar.  This method ONLY supports being invoked on the same thread which originated the log
        /// message.</remarks>
        /// <param name="severity">The severity enum value of the log message.</param>
        /// <param name="writeMode">A LogWriteMode enum value indicating whether to simply queue the log message
        /// and return quickly, or to wait for the log message to be committed to disk before returning.</param>
        /// <param name="logSystem">The name of the originating log system, such as "Trace", "Log4Net",
        /// or "Gibraltar".</param>
        /// <param name="categoryName">The logging category or application subsystem category that the log message
        /// is associated with, such as "Trace", "Console", "Exception", or the logger name in Log4Net.</param>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message.</param>
        /// <param name="userName">The effective username associated with the execution task which
        /// issued the log message.</param>
        /// <param name="exception">An Exception object attached to this log message, or null if none.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multi-line description to use which can be a format string for the arguments.  Can be null.</param>
        /// <param name="args">Optional.  A variable number of arguments to insert into the formatted description string.</param>
        void WriteMessage(LogMessageSeverity severity, LogWriteMode writeMode, string logSystem,
            string categoryName, IMessageSourceProvider sourceProvider, string userName,
            Exception exception, string detailsXml, string caption, string description, params object[] args);

        /// <summary>
        /// End the current log file (but not the session) and open a new file to continue logging.
        /// </summary>
        /// <remarks>This method is provided to support user-initiated roll-over to a new log file
        /// (instead of waiting for an automatic maintenance roll-over) in order to allow the logs of
        /// an ongoing session up to that point to be collected and submitted (or opened in the viewer)
        /// for analysis without shutting down the subject application.</remarks>
        void EndFile();

        /// <summary>
        /// End the current log file (but not the session) and open a new file to continue logging.
        /// </summary>
        /// <remarks>This method is provided to support user-initiated roll-over to a new log file
        /// (instead of waiting for an automatic maintenance roll-over) in order to allow the logs of
        /// an ongoing session up to that point to be collected and submitted (or opened in the viewer)
        /// for analysis without shutting down the subject application.</remarks>
        /// <param name="reason">An optionally-declared reason for invoking this operation (may be null or empty).</param>
        void EndFile(string reason);

        /// <summary>
        /// End the current log file (but not the session) and open a new file to continue logging.
        /// </summary>
        /// <remarks>This method is provided to support user-initiated roll-over to a new log file
        /// (instead of waiting for an automatic maintenance roll-over) in order to allow the logs of
        /// an ongoing session up to that point to be collected and submitted (or opened in the viewer)
        /// for analysis without shutting down the subject application.</remarks>
        /// <param name="skipFrames">The number of stack frames to skip out to find the original caller.</param>
        /// <param name="reason">An optionally-declared reason for invoking this operation (may be null or empty).</param>
        void EndFile(int skipFrames, string reason);

        /// <summary>
        /// Called at the end of the process execution cycle to indicate that the process shut down normally or explicitly crashed.
        /// </summary>
        /// <remarks><para>This will put the Gibraltar log into an ending state in which it will flush everything still
        /// in its queue and then switch to a background thread to process any further log messages.  All log messages
        /// submitted after this call will block the submitting thread until they are committed to disk, so that any
        /// foreground thread still logging final items will be sure to get them through before they exit.  This is
        /// called automatically when an ApplicationExit event is received, and can also be called directly (such as
        /// if that event would not function).</para>
        /// <para>If EndSession is never called, the log will reflect that the session must have crashed.</para></remarks>
        /// <param name="endingStatus">The explicit ending status to declare for this session, <see cref="SessionStatus.Normal">Normal</see>
        /// or <see cref="SessionStatus.Crashed">Crashed</see>.</param>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message.</param>
        /// <param name="reason">A simple reason to declare why the application is ending as Normal or as Crashed, or may be null.</param>
        void EndSession(SessionStatus endingStatus, IMessageSourceProvider sourceProvider, string reason);

        /// <summary>
        /// Called at the end of the process execution cycle to indicate that the process shut down normally or explicitly crashed.
        /// </summary>
        /// <remarks><para>This will put the Gibraltar log into an ending state in which it will flush everything still
        /// in its queue and then switch to a background thread to process any further log messages.  All log messages
        /// submitted after this call will block the submitting thread until they are committed to disk, so that any
        /// foreground thread still logging final items will be sure to get them through before they exit.  This is
        /// called automatically when an ApplicationExit event is received, and can also be called directly (such as
        /// if that event would not function).</para>
        /// <para>If EndSession is never called, the log will reflect that the session must have crashed.</para></remarks>
        /// <param name="endingStatus">The explicit ending status to declare for this session, <see cref="SessionStatus.Normal">Normal</see>
        /// or <see cref="SessionStatus.Crashed">Crashed</see>.</param>
        /// <param name="skipFrames">The number of stack frames to skip out to find the original caller.</param>
        /// <param name="reason">A simple reason to declare why the application is ending as Normal or as Crashed, or may be null.</param>
        void EndSession(SessionStatus endingStatus, int skipFrames, string reason);

        /// <summary>
        /// Called to activate the logging system.  If it is already active then this has no effect.
        /// </summary>
        /// <param name="configuration">Optional.  An initial default configuration to use instead of the configuration file.</param>
        /// <param name="skipFrames"></param>
        /// <param name="reason"></param>
        void StartSession(AgentConfiguration configuration, int skipFrames, string reason);

        /// <summary>
        /// Called to activate the logging system.  If it is already active then this has no effect.
        /// </summary>
        /// <param name="configuration">Optional.  An initial default configuration to use instead of the configuration file.</param>
        /// <param name="sourceProvider"></param>
        /// <param name="reason"></param>
        void StartSession(AgentConfiguration configuration, IMessageSourceProvider sourceProvider, string reason);

        /// <summary>
        /// Send sessions using packager
        /// </summary>
        /// <param name="criteria">Optional.  A session criteria to use</param>
        /// <param name="sessionMatchPredicate">Optional.  A session match predicate to use</param>
        /// <param name="asyncSend"></param>
        /// <returns>True if the send was processed, false if it was not due to configuration or another active send</returns>
        /// <remarks>Either a criteria or sessionMatchPredicate must be provided</remarks>
        Task<bool> SendSessions(SessionCriteria? criteria, Predicate<ISessionSummary> sessionMatchPredicate, bool asyncSend);

        /// <summary>
        /// Set the SendSessionsOnExit setting.  (Should only be called through the SendSessionsOnExit property in Monitor.Log or Agent.Log.)
        /// </summary>
        /// <param name="value"></param>
        void SetSendSessionsOnExit(bool value);

        /// <summary>
        /// Create a complete log message WITHOUT sending it to the Gibraltar central log.
        /// </summary>
        /// <remarks>This method is used internally to construct a complete LogMessagePacket, which can then be
        /// bundled with other packets (in an array) to be submitted to the log as a batch.  This method ONLY
        /// supports being invoked on the same thread which is originating the log message.</remarks>
        /// <param name="severity">The severity enum value of the log message.</param>
        /// <param name="logSystem">The name of the originating log system, such as "Trace", "Log4Net",
        /// or "Gibraltar".</param>
        /// <param name="category">The application subsystem or logging category that the log message is associated with,
        /// which can be a dot-delimited hierarchy (e.g. the logger name in log4net).</param>
        /// <param name="sourceProvider">An IMessageSourceProvider object which supplies the source information
        /// about this log message.</param>
        /// <param name="userName">The effective username associated with the execution task which
        /// issued the log message.</param>
        /// <param name="exception">An Exception object attached to this log message, or null if none.</param>
        /// <param name="detailsXml">Optional.  An XML document with extended details about the message.  Can be null.</param>
        /// <param name="caption">A single line display caption.</param>
        /// <param name="description">Optional.  A multi-line description to use which can be a format string for the arguments.  Can be null.</param>
        /// <param name="args">A variable number of arguments to insert into the formatted description string.</param>
        IMessengerPacket MakeLogPacket(LogMessageSeverity severity, string logSystem, string category,
            IMessageSourceProvider sourceProvider, string userName, Exception exception,
            string detailsXml, string caption, string description, params object[] args);

        /// <summary>
        /// Publish the provided raw packet to the stream.
        /// </summary>
        /// <remarks>This functionality is primarily for internal support of the various log listeners that support
        /// third party log systems.  This overload uses the default LogWriteMode.Queued.  To specify wait-for-commit
        /// behavior, use the overload with a LogWriteMode argument.</remarks>
        /// <param name="packet">The log packet to write</param>
        void Write(IMessengerPacket packet);

        /// <summary>
        /// Publish a batch of raw packets to the stream, specifying the LogWriteMode to use.
        /// </summary>
        /// <remarks>This functionality is primarily for internal support of the various log listeners that support
        /// third party log systems.</remarks>
        /// <param name="packetArray">An array of the log packets to write.</param>
        /// <param name="writeMode">Whether to queue-and-return or wait-for-commit.</param>
        void Write(IMessengerPacket[] packetArray, LogWriteMode writeMode);

        /// <summary>
        /// Raised whenever the log system is being started to enable programmatic configuration.
        /// </summary>
        /// <remarks>You can cancel initialization by setting the cancel property to true in the event arguments. 
        /// If canceled, the log system will not record any information but allow all calls to be made.
        /// Even if canceled it is possible for the logging system to attempt to reinitialize if a call 
        /// is explicitly made to start a session.</remarks>
        event Log.InitializingEventHandler Initializing;
    }
}