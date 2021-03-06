using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using RedStapler.StandardLibrary.Caching;
using RedStapler.StandardLibrary.Configuration;
using RedStapler.StandardLibrary.DataAccess;
using RedStapler.StandardLibrary.Email;
using RedStapler.StandardLibrary.Encryption;
using RedStapler.StandardLibrary.EnterpriseWebFramework;
using RedStapler.StandardLibrary.EnterpriseWebFramework.Controls;
using RedStapler.StandardLibrary.EnterpriseWebFramework.UserManagement;

namespace RedStapler.StandardLibrary {
	/// <summary>
	/// Provides a suite of static methods to make database connection operations easy.
	/// </summary>
	public static partial class AppTools {
		private static bool initialized;
		private static SystemInitializer globalInitializer;
		private static bool secondaryInitFailed;

		/// <summary>
		/// Initializes the class. This includes loading application settings from the configuration file. The application name should be scoped within the system.
		/// For non web applications, this method must be called directly from the main executable assembly and not from a supporting library.
		/// 
		/// To debug this method, create a folder called C:\AnyoneFullControl and give Everyone full control. A file will appear in that folder explaining how far
		/// it got in init.
		/// </summary>
		/// <param name="globalInitializer">The system's global initializer. Do not pass null.</param>
		/// <param name="appName"></param>
		/// <param name="isClientSideProgram"></param>
		/// <param name="useRelativeInstallationPath">Pass true to use a relative path for the installation folder. This means that the folder will be located using
		/// the working directory rather than the assembly path. Use with caution.</param>
		/// <param name="mainDataAccessStateGetter">A method that returns the current main data-access state whenever it is requested, including during this
		/// AppTools.Init call. Do not allow multiple threads to use the same state at the same time. If you pass null, the data-access subsystem will not be
		/// available in the application.</param>
		public static void Init(
			SystemInitializer globalInitializer, string appName, bool isClientSideProgram, bool useRelativeInstallationPath = false,
			Func<DataAccessState> mainDataAccessStateGetter = null ) {
			var initializationLog = "Starting init";
			try {
				if( initialized )
					throw new ApplicationException( "This class can only be initialized once." );

				if( globalInitializer == null )
					throw new ApplicationException( "The system must have a global initializer and you must pass an instance of it to AppTools.Init." );

				// Initialize ConfigurationStatics, including the general provider, before the exception handling block below because it's reasonable for the exception
				// handling to depend on this.
				ConfigurationStatics.Init( useRelativeInstallationPath, globalInitializer.GetType(), appName, isClientSideProgram, ref initializationLog );

				// Setting the initialized flag to true must be done before executing the secondary init block below so that exception handling works.
				initialized = true;
				initializationLog += Environment.NewLine + "Succeeded in primary init.";
			}
			catch( Exception e ) {
				initializationLog += Environment.NewLine + e;
				StandardLibraryMethods.EmergencyLog( "Initialization log", initializationLog );
				throw;
			}

			try {
				var asposeLicense = ConfigurationStatics.SystemGeneralProvider.AsposeLicenseName;
				if( asposeLicense.Any() ) {
					new Aspose.Pdf.License().SetLicense( asposeLicense );
					new Aspose.Words.License().SetLicense( asposeLicense );
				}

				// This initialization could be performed using reflection. There is no need for AppTools to have a dependency on these classes.
				AppMemoryCache.Init();
				BlobFileOps.Init();
				DataAccessStatics.Init();
				DataAccessState.Init( mainDataAccessStateGetter );
				EncryptionOps.Init();
				HtmlBlockStatics.Init();
				InstallationSupportUtility.ConfigurationLogic.Init1();
				UserManagementStatics.Init();

				AppTools.globalInitializer = globalInitializer;
				globalInitializer.InitStatics();
			}
			catch( Exception e ) {
				secondaryInitFailed = true;

				// Suppress all exceptions since they would prevent apps from knowing that primary initialization succeeded. EWF apps need to know this in order to
				// automatically restart themselves. Other apps could find this knowledge useful as well.
				try {
					TelemetryStatics.ReportError( "An exception occurred during application initialization:", e );
				}
				catch {}
			}
		}

		internal static bool SecondaryInitFailed {
			get {
				assertClassInitialized();
				return secondaryInitFailed;
			}
		}

		/// <summary>
		/// Performs cleanup activities so the application can be shut down.
		/// </summary>
		public static void CleanUp() {
			assertClassInitialized();

			try {
				globalInitializer.CleanUpStatics();

				AppMemoryCache.CleanUp();
			}
			catch( Exception e ) {
				TelemetryStatics.ReportError( "An exception occurred during application cleanup:", e );
			}
		}

		/// <summary>
		/// Gets the name of the system.
		/// </summary>
		public static string SystemName {
			get {
				assertClassInitialized();
				return ConfigurationStatics.InstallationConfiguration.SystemName;
			}
		}

		internal static bool IsDevelopmentInstallation {
			get {
				assertClassInitialized();
				return ConfigurationStatics.InstallationConfiguration.InstallationType == InstallationType.Development;
			}
		}

		/// <summary>
		/// Framework use only.
		/// </summary>
		public static bool IsIntermediateInstallation {
			get {
				assertClassInitialized();
				return ConfigurationStatics.InstallationConfiguration.InstallationType == InstallationType.Intermediate;
			}
		}

		/// <summary>
		/// Gets whether this is a live installation. Use with caution. If you do not deliberately test code that only runs in live installations, you may not
		/// discover problems with it until it is live.
		/// </summary>
		public static bool IsLiveInstallation {
			get {
				assertClassInitialized();
				return ConfigurationStatics.InstallationConfiguration.InstallationType == InstallationType.Live;
			}
		}

		internal static bool DatabaseExists {
			get {
				assertClassInitialized();
				return ConfigurationStatics.InstallationConfiguration.PrimaryDatabaseInfo != null;
			}
		}

		/// <summary>
		/// Gets the user object for the authenticated user. Returns null if the user has not been authenticated. In a web application, do not use from the
		/// initDefaultOptionalParameterPackage or init methods of Info classes because the page has not yet been able to correct the connection security of the
		/// request, if necessary, and because parent authorization logic has not yet executed. To use from initUserDefaultOptionalParameterPackage you must
		/// explicitly specify the connection security as SecureIfPossible in all pages and entity setups that use this item as a parent. To use from
		/// createParentPageInfo--which you should only do if, for a given set of parameters, there is no single parent that all users can access--you must
		/// explicitly specify the connection security as SecureIfPossible in the current item. With both of these uses, keep in mind that no parent authorization
		/// logic has executed and therefore you cannot assume anything about the user. Does not currently work outside of web applications.
		/// </summary>
		public static User User {
			get {
				assertClassInitialized();
				return EwfApp.Instance != null && EwfApp.Instance.RequestState != null ? EwfApp.Instance.RequestState.UserAndImpersonator.Item1 : null;
			}
		}

		[ Obsolete( "Guaranteed through 31 July 2015. Please use TelemetryStatics.ReportError instead." ) ]
		public static void EmailAndLogError( Exception e ) {
			TelemetryStatics.ReportError( e );
		}

		[ Obsolete( "Guaranteed through 31 July 2015. Please use TelemetryStatics.ReportError instead." ) ]
		public static void EmailAndLogError( string prefix, Exception exception ) {
			TelemetryStatics.ReportError( prefix, exception );
		}

		[ Obsolete( "Guaranteed through 31 July 2015. Please use EmailStatics.SendEmailWithDefaultFromAddress instead." ) ]
		public static void SendEmailWithDefaultFromAddress( EmailMessage m ) {
			assertClassInitialized();
			EmailStatics.SendEmailWithDefaultFromAddress( m );
		}

		[ Obsolete( "Guaranteed through 31 March 2015. Please use EmailStatics.SendEmail instead." ) ]
		public static void SendEmail( EmailMessage message ) {
			assertClassInitialized();
			EmailStatics.SendEmail( message );
		}

		/// <summary>
		/// Executes the specified method. Returns 0 if it is successful. If an exception occurs, this method returns 1 and details about the exception are emailed
		/// to the developers and logged. This should only be used at the root level of a console application because it checks to ensure the system logic has
		/// initialized properly and its return code is designed to be useful from the command line of such an application. Throw a DoNotEmailOrLogException to
		/// cause this method to return 1 without emailing or logging the exception.
		/// </summary>
		public static int ExecuteAppWithStandardExceptionHandling( Action method ) {
			assertClassInitialized();

			if( secondaryInitFailed )
				return 1;
			try {
				method();
			}
			catch( Exception e ) {
				if( !( e is DoNotEmailOrLogException ) )
					TelemetryStatics.ReportError( e );
				return 1;
			}
			return 0;
		}

		/// <summary>
		/// Executes the specified method. Returns true if it is successful. If an exception occurs, this method returns false and details about the exception are emailed
		/// to the developers and logged. This should not be necessary in web applications, as they have their own error handling.
		/// Prior to calling this, you should be sure that system logic initialization has succeeded.  This almost always means that ExecuteAppWithStandardExceptionHandling
		/// has been called somewhere up the stack.
		/// This method has no side effects other than those of the given method and the email/logging that occurs in the event of an error.  This can be used
		/// repeatedly inside any application as an alternative to a try catch block.
		/// </summary>
		public static bool ExecuteBlockWithStandardExceptionHandling( Action method ) {
			assertClassInitialized();

			try {
				method();
				return true;
			}
			catch( Exception e ) {
				TelemetryStatics.ReportError( e );
				return false;
			}
		}

		/// <summary>
		/// Use this to email errors from web service methods and turn normal exceptions into FaultExceptions.
		/// </summary>
		public static void ExecuteWebServiceWithStandardExceptionHandling( Action method ) {
			assertClassInitialized();

			// NOTE: Do we need to check whether the system logic was initialized properly or will EwfApp_BeginRequest take care of it?
			try {
				method();
			}
			catch( Exception e ) {
				throw createWebServiceException( e );
			}
		}

		/// <summary>
		/// Use this to email errors from web service methods and turn normal exceptions into FaultExceptions.
		/// </summary>
		public static T ExecuteWebServiceWithStandardExceptionHandling<T>( Func<T> method ) {
			assertClassInitialized();

			// NOTE: Do we need to check whether the system logic was initialized properly or will EwfApp_BeginRequest take care of it?
			try {
				return method();
			}
			catch( Exception e ) {
				throw createWebServiceException( e );
			}
		}

		private static Exception createWebServiceException( Exception e ) {
			if( !( e is Wcf.AccessDeniedException ) )
				TelemetryStatics.ReportError( e );
			return new FaultException( e.ToString() );
		}

		/// <summary>
		/// Executes the given block of code as a critical region synchronized on the given GUID. The GUID should be passed with surrounding {}.
		/// The GUID is automatically prefixed with Global\ so that the mutex has machine scope. The GUID will usually be one to one with a program.
		/// Pass true for SkipExecutionIfMutexAlreadyOwned to return if something already has the mutex.  This is useful for killing a program when
		/// you only want one instance to run at a time. Pass false if you want to wait until the mutex is released to run your code.
		/// Returns false if execution was skipped.  Otherwise, returns true.
		/// If using this along with a WithStandardExceptionHandling method, this should go inside.
		/// </summary>
		public static bool ExecuteAsCriticalRegion( string guid, bool skipExecutionIfMutexAlreadyOwned, Action method ) {
			// The Global\ prefix makes the mutex visible across terminal services sessions. The double backslash is convention.
			// NOTE: What double backslash? Isn't it a single backslash as the comment states?
			guid = "Global\\" + guid;

			using( var mutex = new Mutex( false /*Do not try to immediately acquire the mutex*/, guid ) ) {
				if( skipExecutionIfMutexAlreadyOwned ) {
					try {
						if( !mutex.WaitOne( 0 ) )
							return false;
					}
					catch( AbandonedMutexException ) {}
				}

				try {
					// AbandonedMutexException exists to warn us that data might be corrupt because another thread didn't properly release the mutex. We ignore it because
					// in our case, we only use the mutex in one thread per process (NOTE: This is true, but only by coincidence) and therefore don't need to worry about data corruption.
					// AbandonedMutexExceptions are thrown when the mutex is acquired, not when it is abandoned. Therefore, only the one thread that acquires the mutex
					// next will have to deal with the exception. For this reason, we are OK here in terms of only letting one thread execute its method at a time.
					try {
						// Acquire the mutex, waiting if necessary.
						mutex.WaitOne();
					}
					catch( AbandonedMutexException ) {}

					method();
				}
				finally {
					// We release the mutex manually since, yet again, nobody can agree on whether the Dispose() method called at the end of the using block always properly
					// does this for us.  Some have reported you need to do what we are doing here, so for safety's sake, we have our own finally block.

					mutex.ReleaseMutex();
				}
			}
			return true;
		}

		/// <summary>
		/// Executes the given block of code and returns the time it took to execute.
		/// </summary>
		public static TimeSpan ExecuteTimedRegion( Action method ) {
			var chrono = new Chronometer();
			method();
			return chrono.Elapsed;
		}

		[ Obsolete( "Guaranteed through 31 July 2015. Please use EmailStatics.GetAdministratorEmailAddresses instead." ) ]
		public static List<EmailAddress> AdministratorEmailAddresses { get { return EmailStatics.GetAdministratorEmailAddresses().ToList(); } }

		internal static string CertificateEmailAddressOverride {
			get {
				assertClassInitialized();
				return ConfigurationStatics.InstallationConfiguration.CertificateEmailAddressOverride;
			}
		}

		/// <summary>
		/// Gets the path of the Files folder for the system.
		/// </summary>
		public static string FilesFolderPath {
			get {
				assertClassInitialized();
				return
					StandardLibraryMethods.CombinePaths(
						InstallationFileStatics.GetGeneralFilesFolderPath(
							ConfigurationStatics.InstallationConfiguration.InstallationPath,
							ConfigurationStatics.InstallationConfiguration.InstallationType == InstallationType.Development ),
						InstallationFileStatics.FilesFolderName );
			}
		}

		/// <summary>
		/// Development Utility use only.
		/// </summary>
		public static string InstallationPath {
			get {
				assertClassInitialized();
				return ConfigurationStatics.InstallationConfiguration.InstallationPath;
			}
		}

		/// <summary>
		/// Development Utility use only.
		/// </summary>
		public static string ConfigurationFolderPath {
			get {
				assertClassInitialized();
				return ConfigurationStatics.InstallationConfiguration.ConfigurationFolderPath;
			}
		}

		/// <summary>
		/// Development Utility use only.
		/// </summary>
		public static string ServerSideConsoleAppRelativeFolderPath {
			get {
				return ConfigurationStatics.InstallationConfiguration.InstallationType == InstallationType.Development
					       ? StandardLibraryMethods.GetProjectOutputFolderPath( true )
					       : "";
			}
		}

		private static void assertClassInitialized() {
			if( !initialized )
				throw new ApplicationException( "Initialize the class before calling this method." );
		}
	}
}