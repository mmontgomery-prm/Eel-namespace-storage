using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;

using EEL_Demo.EEL_LogEnterpriseErrorWS;
using DefList = EEL_Demo.EEL_GetDefinitionList;

/* Implementation requirements:
 *
 *      Define exception handling for invalid file path to log to cache
 *      Define call back handler for Async logging calls
 *      
 */

namespace EEL_Demo.Library
{
    public interface IEnterpriseErrorLogger
    {
        EnterpriseErrorResponse LogError(string enterpriseErrorCode, Exception ex, bool AsyncFlag = false);
        EnterpriseErrorResponse LogError(EnterpriseError error, bool AsyncFlag = false);
        EnterpriseErrorResponse LogError(string enterpriseErrorCode, bool AsyncFlag = false, string applicationErrorCode = null, string applicationException = null, string applicationParams = null);
        void FlushLogCacheToDisk();
    }

    public class EnterpriseErrorLogger : IEnterpriseErrorLogger
    {
        private Dictionary<string, string> enterprise_error_dict;
        private Dictionary<int, EnterpriseError> ErrorCache = new Dictionary<int, EnterpriseError>();

        /// <summary>
        /// 
        /// </summary>
        public EnterpriseErrorLogger()
        {
            enterprise_error_dict = this.LoadEnterpriseErrorList();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="enterpriseErrorCode"></param>
        /// <param name="ex"></param>
        /// <param name="AsyncFlag"></param>
        /// <returns></returns>
        public EnterpriseErrorResponse LogError(string enterpriseErrorCode, Exception ex, bool AsyncFlag = false )
        {
            EnterpriseError t_err = new EnterpriseError()
            {
                EnterpriseErrorCode = enterpriseErrorCode,
                ApplicationErrorCode = ex.TargetSite.Name,
                ApplicationName = EnterpriseErrorLoggerConstants.ApplicationName,
                ApplicationException = ex.Message,
                ApplicationParameters = ex.Source,
                ApplicationServer = EnterpriseErrorLoggerConstants.ApplicationServer(),
                ApplicationErrorDateTime = DateTime.Now
            };

            return CommitError(t_err, AsyncFlag);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        /// <param name="AsyncFlag"></param>
        /// <returns></returns>
        public EnterpriseErrorResponse LogError(EnterpriseError error, bool AsyncFlag = false)
        {
            EnterpriseErrorResponse response = new EnterpriseErrorResponse();
            EnterpriseError t_err = new EnterpriseError()
            {
                EnterpriseErrorCode = error.EnterpriseErrorCode,
                ApplicationErrorCode = error.ApplicationErrorCode,
                ApplicationName = error.ApplicationName,
                ApplicationException = error.ApplicationException,
                ApplicationParameters = error.ApplicationParameters,
                ApplicationServer = error.ApplicationServer,
                ApplicationErrorDateTime = error.ApplicationErrorDateTime
            };

            return ((response = CommitError(t_err, AsyncFlag)) != null) ? response : StringToEnterpriseErrorResponse(error.EnterpriseErrorCode,
                                                                                                                    this.GetEnterpriseErrorUserMessage(error.EnterpriseErrorCode));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="enterpriseErrorCode"></param>
        /// <param name="AsyncFlag"></param>
        /// <param name="applicationErrorCode"></param>
        /// <param name="applicationException"></param>
        /// <param name="applicationParams"></param>
        /// <returns></returns>
        public EnterpriseErrorResponse LogError(string enterpriseErrorCode, bool AsyncFlag = false, string applicationErrorCode = null, string applicationException = null, string applicationParams = null)
        {
            EnterpriseError t_err = new EnterpriseError()
            {
                EnterpriseErrorCode = enterpriseErrorCode,
                ApplicationErrorCode = applicationErrorCode,
                ApplicationName = EnterpriseErrorLoggerConstants.ApplicationName,
                ApplicationException = applicationException,
                ApplicationParameters = applicationParams,
                ApplicationServer = EnterpriseErrorLoggerConstants.ApplicationServer(),
                ApplicationErrorDateTime = DateTime.Now
            };

            return CommitError(t_err, AsyncFlag);
        }

        /// <summary>
        /// 
        /// </summary>
        public void FlushLogCacheToDisk()
        {
            try
            {
                string t_logpath = EnterpriseErrorLoggerConstants.CacheLogPath();
                ErrorCache = HttpContext.Current.Session[EnterpriseErrorLoggerConstants.CacheSessionKeyName] as Dictionary<int, EnterpriseError>;

                if (ErrorCache != null || ErrorCache.Any())
                {
                    //StreamWriter oStream = new StreamWriter(HttpContext.Current.Server.MapPath(EnterpriseErrorLoggerConstants.CacheLogPath()));
                    StreamWriter oStream = new StreamWriter(t_logpath);
                    this.ToStream(oStream);
                    oStream.Close();
                }
            }
            catch (Exception e) 
            {
                if ( !EnterpriseErrorLoggerConstants.ContinueOnFail )
                    throw e;
            }
        }

        private string GetEnterpriseErrorUserMessage(string aCode)
        {
            if (enterprise_error_dict != null
                && enterprise_error_dict.ContainsKey(aCode))
                return enterprise_error_dict[aCode];

            return enterprise_error_dict[ErrorCodes.SYS_LOGGING_FAILURE];
        }

        private void LogInCache(EnterpriseError error)
        {
            if (HttpContext.Current.Session[EnterpriseErrorLoggerConstants.CacheSessionKeyName] == null)
            {
                ErrorCache.Add(ErrorCache.Count, error);
                HttpContext.Current.Session[EnterpriseErrorLoggerConstants.CacheSessionKeyName] = ErrorCache;
            }
            else
            {
                Dictionary<int, EnterpriseError> errors = HttpContext.Current.Session[EnterpriseErrorLoggerConstants.CacheSessionKeyName] as Dictionary<int, EnterpriseError>;

                if ( errors.Count >= EnterpriseErrorLoggerConstants.LogCacheSize )
                {
                    this.FlushLogCacheToDisk();
                    errors.Clear();
                }
                errors.Add(errors.Count, error);
            }
        }

        private void ToStream(StreamWriter oStream)
        {
            string m_string = String.Join( EnterpriseErrorLoggerConstants.LineSeperator, 
                                              ErrorCache.Values.ToList().Select(y => y.ToLogCacheString()) );

            oStream.Write(m_string);
        }

        private Dictionary<string, string> LoadEnterpriseErrorList()
        {
            var t_dict = new Dictionary<string, string>();

            try
            {
                return enterprise_error_dict = CallDefinitionListWebService().ToDictionary(x => x.EnterpriseErrorCode, x => x.EnterpriseErrorUserMessage);
            }
            catch
            {
                t_dict.Add(ErrorCodes.SYS_LOGGING_FAILURE, EnterpriseErrorLoggerConstants.DefaultErrorDefinitionMessage);
                return enterprise_error_dict = t_dict;
            }
        }

        private IEnumerable<DefList.EnterpriseErrorDefinitionList>  CallDefinitionListWebService( )
        {
            var defListRequest = new DefList.EnterpriseErrorDefinitionListRequest()
            {
                ApplicationID = EnterpriseErrorLoggerConstants.ApplicationID
            };

            var errorResponse = new DefList.Provide_ServiceResponse().EnterpriseErrorDefinitionListResponse;
            var portClient = new DefList.Provide_ServicePortClient();
            var listRequest = new DefList.Provide_ServiceRequest();

            listRequest.EnterpriseErrorDefinitionListRequest = defListRequest;
            errorResponse = portClient.Provide_Service(listRequest.EnterpriseErrorDefinitionListRequest);
            return errorResponse;
        }

        //private EnterpriseErrorResponse CommitError(EnterpriseError error, bool AsyncFlag = false)
        //{       
        //    EnterpriseErrorResponse result = null;
        //    try
        //    {
        //        if (AsyncFlag)
        //        {
        //            var errorResponse = new EnterpriseErrorResponse();
        //            var portClient = new Provide_ServicePortClient();
        //            var errorRequest = new Provide_ServiceRequest();

        //            errorRequest.EnterpriseErrorRequest = error;
        //            return portClient.Provide_Service(errorRequest.EnterpriseErrorRequest);
        //        }
        //        else
        //        {
        //            var errorResponse = new EnterpriseErrorResponse();
        //            var portClient = new Provide_ServicePortClient();
        //            var errorRequest = new Provide_ServiceRequest();

        //            errorRequest.EnterpriseErrorRequest = error;
        //            return portClient.Provide_Service(errorRequest.EnterpriseErrorRequest);
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        LogInCache(error);
        //    }

        //    return result;
        //}

        private EnterpriseErrorResponse CommitError(EnterpriseError error, bool AsyncFlag = false)
        {
            EnterpriseErrorResponse result = null;
            try
            {
                if (AsyncFlag)
                {
                    AsyncServiceCall(error);
                }
                else
                {
                    var errorResponse = new EnterpriseErrorResponse();
                    var portClient = new Provide_ServicePortClient();
                    var errorRequest = new Provide_ServiceRequest();

                    errorRequest.EnterpriseErrorRequest = error;
                    return portClient.Provide_Service(errorRequest.EnterpriseErrorRequest);
                }
            }
            catch (Exception e)
            {
                LogInCache(error);
            }

            return result;
        }

        private void AsyncServiceCall(EnterpriseError error)
        {
            EnterpriseErrorResponse result = null;

            var errorResponse = new EnterpriseErrorResponse();
            var portClient = new Provide_ServicePortClient();
            var errorRequest = new Provide_ServiceRequest();

            portClient.Provide_ServiceCompleted += new EventHandler<Provide_ServiceCompletedEventArgs>((o, e) =>
            {
                try
                {
                    if (e.Result.ResponseStatus.ToString() == errorResponse.ResponseStatus.ToString())
                    {
                        result = e.Result;
                    }
                    else
                    {
                        var a = "test";
                    }
                }
                catch (Exception ex)
                {
                    LogInCache(error);
                    this.FlushLogCacheToDisk();
                }
            });

            errorRequest.EnterpriseErrorRequest = error;
            portClient.Provide_ServiceAsync(errorRequest.EnterpriseErrorRequest);
        }

        private EnterpriseErrorResponse StringToEnterpriseErrorResponse(string aCode, string aMessage)
        {
            return new EnterpriseErrorResponse()
            {
                ResponseStatus = EnterpriseErrorResponseResponseStatus.FAILED,
                EnterpriseErrorCode = aCode,
                EnterpriseErrorString = aMessage
            };
        }
    }

    public static class EnterpriseErrorExtensions
    {
        public static string ToLogCacheString(this EnterpriseError err)
        {
            return String.Format(
                    "Error Cache Entry: {0} | {1} | {2} | {3} | {4} | {5} | {6}\r\n",
                    err.EnterpriseErrorCode.ToString(),
                    err.ApplicationErrorCode.ToString(),
                    err.ApplicationName.ToString(),
                    err.ApplicationException.ToString(),
                    err.ApplicationParameters.ToString(),
                    err.ApplicationServer.ToString(),
                    err.ApplicationErrorDateTime.ToString()
                );
        }
    }
public static class ErrorCodes 
{
public const string OST_CLOSEDATE_BEFORE_TODAY_ERROR = "OST_CLOSEDATE_BEFORE_TODAY_ERROR";
public const string OST_SFDC_QUOTE_CREATION_FAILED = "OST_SFDC_QUOTE_CREATION_FAILED";
public const string OST_SFDC_WS_ERROR = "OST_SFDC_WS_ERROR";
public const string SYS_DEBUG_MESSAGE = "SYS_DEBUG_MESSAGE";
public const string SYS_GENERIC_EXCEPTION = "SYS_GENERIC_EXCEPTION";
public const string SYS_INVALID_XML = "SYS_INVALID_XML";
public const string SYS_LOG_MESSAGE = "SYS_LOG_MESSAGE";
public const string SYS_NULL_OBJECT_REFERENCE = "SYS_NULL_OBJECT_REFERENCE";
public const string SYS_TEST_ERROR = "SYS_TEST_ERROR";
public const string SYS_UNKNOWN_ERROR = "SYS_UNKNOWN_ERROR";}

public static class EnterpriseErrorLoggerConstants 
{

                                public const int LogCacheSize = 10;
                                public const string ApplicationName = 'One Sheeter Integrator';
                                public const string LineSeperator = '';
                                public const string CacheSessionKeyName = 'ErrorCache';
                                public static bool ContinueOnFail = true;
                                public const int ApplicationID = 2;
                                public const string DefaultErrorDefinitionMessage = 'Fatal failure in logging.  No definitions loaded.';

                                public static string ApplicationServer()
                                {
                                    return System.Environment.MachineName;
                                }

                                public static string CacheLogPath()
                                {
                                    return String.Format('C:\\EELLogs\\{0}_{1}-{2}-{3}-{4}_{5}_{6}.txt',
                                                    ApplicationName,
                                                    DateTime.Now.Day,
                                                    DateTime.Now.Month,
                                                    DateTime.Now.Year,
                                                    DateTime.Now.Hour,
                                                    DateTime.Now.Minute,
                                                    DateTime.Now.Second);
                                }
                            }}