﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Resources;
using System.Threading;

using RapidSoft.Etl.Logging;
using System.Reflection;

namespace RapidSoft.Etl.Runtime
{
    partial class EtlPackage
    {
        private sealed class _Invoker
        {
            #region Constructors

            public _Invoker()
                : this(null)
            {
            }

            public _Invoker
            (
                IEtlLogger logger
            )
            {
			    Properties.Resources.Culture = Thread.CurrentThread.CurrentUICulture;
                _logger = logger ?? new NullEtlLogger();
            }

            #endregion

            #region Constants

            private const string SECURE_VARIABLE_ESCAPED_VALUE = "*";

            #endregion

            #region Fields

            private readonly IEtlLogger _logger;

            private static readonly Type[] _criticalExceptionTypes = 
            {
                typeof(OutOfMemoryException),
                typeof(AppDomainUnloadedException),
                //typeof(BadImageFormatException),
                typeof(CannotUnloadAppDomainException),
                typeof(ExecutionEngineException),
                typeof(InvalidProgramException),
                //typeof(ThreadAbortException)            
            };

            #endregion

            #region Methods

            #region Invoke methods

            public EtlSession InvokePackage(EtlPackage package)
            {
                return InvokePackage(package, null, null);
            }

            public EtlSession InvokePackage(EtlPackage package, EtlVariableAssignment[] assignments)
            {
                return InvokePackage(package, assignments, GetCurrentUserName());
            }

            public EtlSession InvokePackage(EtlPackage package, EtlVariableAssignment[] assignments, string parentSessionId)
            {
                if (package == null)
                {
                    throw new ArgumentNullException("package");
                }

                if (assignments == null)
                {
                    assignments = new EtlVariableAssignment[0];
                }

                var session = InvokePackageCore(package, assignments, parentSessionId);
                return session;
            }

            private EtlSession InvokePackageCore(EtlPackage package, EtlVariableAssignment[] assignments, string parentSessionId)
            {
                var session = new EtlSession
                {
                    EtlSessionId = Guid.NewGuid().ToString(),
                    EtlPackageId = package.Id,
                    EtlPackageName = package.Name,
                    ParentEtlSessionId = parentSessionId,
                    Status = EtlStatus.Started,
                    UserName = GetCurrentUserName(),
                };

                session.StartDateTime = DateTime.Now;
                session.StartUtcDateTime = session.StartDateTime.ToUniversalTime();

                _logger.LogEtlSessionStart(session);
                _logger.LogEtlMessage
                (
                    new EtlMessage
                    {
                        EtlPackageId = session.EtlPackageId,
                        EtlSessionId = session.EtlSessionId,
                        LogDateTime = session.StartDateTime,
                        LogUtcDateTime = session.StartUtcDateTime,
                        MessageType = EtlMessageType.SessionStart,
                        Text = string.Format(Properties.Resources.SessionStarted, session.EtlSessionId, session.EtlPackageId, session.EtlPackageName)
                    }
                );

                try
                {
                    InvokePackageCatched(package, session, assignments);
                }
                catch (Exception exc)
                {
                    if (IsCriticalException(exc))
                    {
                        throw;
                    }

                    session.Status = EtlStatus.Failed;

                    var errorDateTime = DateTime.Now;
                    _logger.LogEtlMessage
                    (
                        new EtlMessage
                        {
                            EtlPackageId = session.EtlPackageId,
                            EtlSessionId = session.EtlSessionId,
                            LogDateTime = errorDateTime,
                            LogUtcDateTime = errorDateTime.ToUniversalTime(),
                            MessageType = EtlMessageType.Error,
                            Text = exc.Message,
                            StackTrace = exc.StackTrace,
                        }
                    );
                }
                finally
                {
                    session.EndDateTime = DateTime.Now;
                    session.EndUtcDateTime = session.EndDateTime.Value.ToUniversalTime();

                    _logger.LogEtlMessage
                    (
                        new EtlMessage
                        {
                            EtlPackageId = session.EtlPackageId,
                            EtlSessionId = session.EtlSessionId,
                            LogDateTime = session.EndDateTime.Value,
                            LogUtcDateTime = session.EndUtcDateTime.Value,
                            MessageType = EtlMessageType.SessionEnd,
                            Text = string.Format(Properties.Resources.SessionFinished, session.EtlSessionId, session.EtlPackageId, session.EtlPackageName, session.Status)
                        }
                    );

                    _logger.LogEtlSessionEnd(session);
                }

                return session;
            }

            private void InvokePackageCatched(EtlPackage package, EtlSession session, EtlVariableAssignment[] assignments)
            {
                var buildStartDateTime = DateTime.Now;
                _logger.LogEtlMessage
                (
                    new EtlMessage
                    {
                        EtlPackageId = session.EtlPackageId,
                        EtlSessionId = session.EtlSessionId,
                        LogDateTime = buildStartDateTime,
                        LogUtcDateTime = buildStartDateTime.ToUniversalTime(),
                        MessageType = EtlMessageType.Debug,
                        Text = string.Format(Properties.Resources.VariablesInitStarted),
                    }
                );

                var context = new _Context(package, session);
                context.InitVariables(package.Variables, assignments);

                foreach (var variable in context.GetVariables())
                {
                    var escapedVariable = EscapeVariable(variable);

                    _logger.LogEtlMessage
                    (
                        new EtlMessage
                        {
                            EtlPackageId = context.EtlPackageId,
                            EtlSessionId = context.EtlSessionId,
                            LogDateTime = buildStartDateTime,
                            LogUtcDateTime = buildStartDateTime.ToUniversalTime(),
                            MessageType = EtlMessageType.Debug,
                            Text = string.Format(Properties.Resources.VariableInit, escapedVariable.Name, escapedVariable.Value),
                        }
                    );

                    if (variable.Modifier == EtlVariableModifier.Input || variable.Modifier == EtlVariableModifier.Bound)
                    {
                        _logger.LogEtlVariable(escapedVariable);
                    }
                }

                var preprocessor = new EtlPackagePreprocessor();
                package = preprocessor.PreprocessPackage(package, context.GetVariables());

                var buildEndDateTime = DateTime.Now;
                _logger.LogEtlMessage
                (
                    new EtlMessage
                    {
                        EtlPackageId = session.EtlPackageId,
                        EtlSessionId = session.EtlSessionId,
                        LogDateTime = buildEndDateTime,
                        LogUtcDateTime = buildEndDateTime.ToUniversalTime(),
                        MessageType = EtlMessageType.Debug,
                        Text = string.Format(Properties.Resources.VariablesInitFinished),
                    }
                );
                
                InvokePackageSteps(package, context);

                foreach (var variable in context.GetVariables())
                {
                    if (variable.Modifier == EtlVariableModifier.Output)
                    {
                        _logger.LogEtlVariable(EscapeVariable(variable));

                    }
                }

                session.Status = context.CurrentStatus;
            }

            private void InvokePackageSteps(EtlPackage package, _Context context)
            {
                var stepIndex = 0;

                while (stepIndex < package.Steps.Count)
                {
                    var step = package.Steps[stepIndex];


                    var stepResult = InvokePackageStep(step, context);
                    if (EtlStatuses.GetPriorityStatus(stepResult.Status, context.CurrentStatus) == stepResult.Status)
                    {
                        context.CurrentStatus = stepResult.Status;
                    }

                    if (stepResult.Status == EtlStatus.Failed)
                    {
                        break;
                    }

                    if (HasVariableAssignments(stepResult))
                    {
                        var rebuildStartDateTime = DateTime.Now;
                        _logger.LogEtlMessage
                        (
                            new EtlMessage
                            {
                                EtlPackageId = context.EtlPackageId,
                                EtlSessionId = context.EtlSessionId,
                                LogDateTime = rebuildStartDateTime,
                                LogUtcDateTime = rebuildStartDateTime.ToUniversalTime(),
                                MessageType = EtlMessageType.Debug,
                                Text = string.Format(Properties.Resources.VariablesUpdateStarted),
                            }
                        );

                        foreach (var assignment in stepResult.VariableAssignments)
                        {
                            var assignedVariable = context.AssignVariable(assignment);
                            var escapedVariable = EscapeVariable(assignedVariable);

                            _logger.LogEtlMessage
                            (
                                new EtlMessage
                                {
                                    EtlPackageId = context.EtlPackageId,
                                    EtlSessionId = context.EtlSessionId,
                                    LogDateTime = rebuildStartDateTime,
                                    LogUtcDateTime = rebuildStartDateTime.ToUniversalTime(),
                                    MessageType = EtlMessageType.Debug,
                                    Text = string.Format(Properties.Resources.VariableUpdate, escapedVariable.Name, escapedVariable.Value),
                                }
                            );
                        }

                        var preprocessor = new EtlPackagePreprocessor();
                        package = preprocessor.PreprocessPackage(context.OriginalPackage, context.GetVariables());

                        var rebuildEndDateTime = DateTime.Now;
                        _logger.LogEtlMessage
                        (
                            new EtlMessage
                            {
                                EtlPackageId = context.EtlPackageId,
                                EtlSessionId = context.EtlSessionId,
                                LogDateTime = rebuildEndDateTime,
                                LogUtcDateTime = rebuildEndDateTime.ToUniversalTime(),
                                MessageType = EtlMessageType.Debug,
                                Text = string.Format(Properties.Resources.VariablesUpdateFinished),
                            }
                        );
                    }

                    stepIndex++;
                }
            }

            private EtlStepResult InvokePackageStep(EtlStep step, EtlContext context)
            {
                try
                {
                    var startDateTime = DateTime.Now;
                    _logger.LogEtlMessage(new EtlMessage
                    {
                        EtlPackageId = context.EtlPackageId,
                        EtlSessionId = context.EtlSessionId,
                        EtlStepName = step.Name,
                        LogDateTime = startDateTime,
                        LogUtcDateTime = startDateTime.ToUniversalTime(),
                        MessageType = EtlMessageType.StepStart,
                        Text = string.Format(Properties.Resources.StepStarted, step.Name),
                        StackTrace = step.GetType().FullName,
                    });

                    var stepResult = step.Invoke(context, _logger);

                    var endDateTime = DateTime.Now;
                    _logger.LogEtlMessage(new EtlMessage
                    {
                        EtlPackageId = context.EtlPackageId,
                        EtlSessionId = context.EtlSessionId,
                        EtlStepName = step.Name,
                        LogDateTime = endDateTime,
                        LogUtcDateTime = endDateTime.ToUniversalTime(),
                        MessageType = EtlMessageType.StepEnd,
                        Text = string.Format(Properties.Resources.StepFinished, step.Name, stepResult.Status),
                    });

                    return stepResult;
                }
                catch (Exception exc)
                {
                    if (IsCriticalException(exc))
                    {
                        throw;
                    }

                    var errorDateTime = DateTime.Now;
                    _logger.LogEtlMessage
                    (
                        new EtlMessage
                        {
                            EtlPackageId = context.EtlPackageId,
                            EtlSessionId = context.EtlSessionId,
                            EtlStepName = step.Name,
                            LogDateTime = errorDateTime,
                            LogUtcDateTime = errorDateTime.ToUniversalTime(),
                            MessageType = EtlMessageType.Error,
                            Text = exc.Message,
                            StackTrace = exc.StackTrace,
                        }
                    );

                    return new EtlStepResult(EtlStatus.Failed, exc.Message);
                }
            }

            private static bool HasVariableAssignments(EtlStepResult stepResult)
            {
                return (stepResult.VariableAssignments != null) && (stepResult.VariableAssignments.Count > 0);
            }

            private EtlVariable EscapeVariable(EtlVariable variable)
            {
                if (variable == null)
                {
                    return null;
                }

                var escapedVariable = (EtlVariable)variable.Clone();
                if (variable.IsSecure)
                {
                    escapedVariable.Value = SECURE_VARIABLE_ESCAPED_VALUE;
                }

                return escapedVariable;
            }

            #endregion

            #region Security

            private static string GetCurrentUserName()
            {
                if 
                (
                    Thread.CurrentPrincipal != null && 
                    Thread.CurrentPrincipal.Identity != null && 
                    Thread.CurrentPrincipal.Identity.IsAuthenticated
                )
                {
                    return 
                        string.IsNullOrEmpty(Thread.CurrentPrincipal.Identity.Name) 
                        ? null 
                        : Thread.CurrentPrincipal.Identity.Name;
                }
                else
                {
                    return null;
                }            
            }

            #endregion

            #region Exceptions

            private static bool IsCriticalException(Exception exception)
            {
                return IsCriticalException(exception.GetType());
            }

            private static bool IsCriticalException(Type exceptionType)
            {
                foreach (var criticalType in _criticalExceptionTypes)
                {
                    if (criticalType == exceptionType)
                    {
                        return true;
                    }
                }

                return false;
            }

            #endregion

            #endregion
        }
    }
}
