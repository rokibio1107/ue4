﻿// Copyright Epic Games, Inc. All Rights Reserved.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Gauntlet
{
	/// <summary>
	/// Result for a single automation result
	/// </summary>
	public class AutomationTestResult
	{
		/// <summary>
		/// Short friendly name of this test
		/// </summary>
		public string		DisplayName;

		/// <summary>
		/// Full test name
		/// </summary>
		public string		TestName;

		/// <summary>
		/// True if the test completed (pass or fail)
		/// </summary>
		public bool			Completed;

		/// <summary>
		/// True if the test passed
		/// </summary>
		public bool			Passed;

		/// <summary>
		/// Events logged during the test. Should contain errors if the test failed
		/// </summary>
		public IEnumerable<string> Events;

		/// <summary>
		/// Returns all events that are a warning
		/// </summary>
		public IEnumerable<string> WarningEvents 
		{  
			get
			{
				return Events.Where(E => E.ToLower().Contains(": warning:"));
			}
		}

		/// <summary>
		/// Returns all events that are a error
		/// </summary>
		public IEnumerable<string> ErrorEvents
		{
			get
			{
				return Events.Where(E => E.ToLower().Contains(": error:"));
			}
		}

		/// <summary>
		/// Returns all events that are a error
		/// </summary>
		public IEnumerable<string> WarningAndErrorEvents
		{
			get
			{
				return Events.Where(E => E.ToLower().Contains(": error:") || E.ToLower().Contains(": warning:"));
			}
		}

		/// <summary>
		/// Returns the name of the test. If DisplayName and TestName differ they are joined
		/// </summary>
		public string FullName
		{
			get
			{
				string FullName = DisplayName;

				if (TestName != DisplayName)
				{
					FullName += string.Format(": {0}", TestName);
				}

				return FullName;
			}
		}

		/// <summary>
		/// Result contstructor
		/// </summary>
		/// <param name="InDisplayName"></param>
		/// <param name="InTestName"></param>
		/// <param name="bInPassed"></param>
		public AutomationTestResult(string InDisplayName, string InTestName, bool InCompleted, bool bInPassed, IEnumerable<string> InEvents)
		{
			DisplayName = InDisplayName;
			TestName = InTestName;
			Completed = InCompleted;
			Passed = bInPassed;
			Events = InEvents.ToArray();
		}


	};

	/// <summary>
	/// Helper class for parsing AutomationTest results from either an UnrealLogParser or log contents
	/// </summary>
	public class AutomationLogParser
	{
		protected UnrealLogParser Parser;

		public string AutomationReportPath { get; protected set; } = "";

		public string AutomationReportURL { get; protected set; } = "";

		/// <summary>
		/// Returns entries in the log file related to automation
		/// </summary>
		public IEnumerable<UnrealLogParser.LogEntry> AutomationLogEntries { get; protected set; }
	
		/// <summary>
		/// Returns warning/errors in the logfile related to automation
		/// </summary>
		public IEnumerable<UnrealLogParser.LogEntry> AutomationWarningsAndErrors
		{
			get
			{
				return AutomationLogEntries.Where(E => E.Level == UnrealLogParser.LogLevel.Error || E.Level == UnrealLogParser.LogLevel.Warning);
			}
		}

		/// <summary>
		/// Constructor that uses an existing log parser
		/// </summary>
		/// <param name="InParser"></param>
		public AutomationLogParser(UnrealLogParser InParser)
		{
			Parser = InParser;

			IEnumerable<Match> ReportPathMatch = Parser.GetAllMatches(@"LogAutomationController.+Report can be opened.+'(.+)'");

			if (ReportPathMatch.Any())
			{
				AutomationReportPath = Path.GetFullPath(ReportPathMatch.First().Groups[1].ToString());
			}
			IEnumerable<Match> ReportUrlMatch = Parser.GetAllMatches(@"LogAutomationController.+Report can be viewed.+'(.+)'");

			if (ReportUrlMatch.Any())
			{
				AutomationReportURL = ReportUrlMatch.First().Groups[1].ToString();
			}

			AutomationLogEntries = Parser.Entries.Where(
										E => E.Category.StartsWith("Automation", StringComparison.OrdinalIgnoreCase)
										|| E.Category.StartsWith("FunctionalTest", StringComparison.OrdinalIgnoreCase)
										)
									.ToArray();
		}

		/// <summary>
		/// Constructor that takes raw log contents
		/// </summary>
		/// <param name="InContents"></param>
		public AutomationLogParser(string InContents)
				: this(new UnrealLogParser(InContents))
		{
		}
		
		/// <summary>
		/// Returns all results found in our construction content.
		/// </summary>
		/// <returns></returns>
		public IEnumerable<AutomationTestResult> GetResults()
		{
			IEnumerable<Match> TestStarts = Parser.GetAllMatches(@"LogAutomationController.+Test Started. Name={(.+?)}");

			// Find all automation results that succeeded/failed
			// [00:10:54.148]   LogAutomationController: Display: Test Started. Name={ST_PR04}
			// [2019.04.30-18.49.51:329][244]LogAutomationController: Display: Test Completed With Success. Name={ST_PR04} Path={Project.Functional Tests./Game/Tests/Rendering/PlanarReflection.ST_PR04}
			// [2019.04.30-18.49.51:330] [244] LogAutomationController: BeginEvents: Project.Functional Tests./Game/Tests/Rendering/PlanarReflection.ST_PR04
			// [2019.04.30 - 18.49.51:331][244] LogAutomationController: Screenshot 'ST_PR04' was similar!  Global Difference = 0.001377, Max Local Difference = 0.037953
			// [2019.04.30 - 18.49.51:332][244]LogAutomationController: EndEvents: Project.Functional Tests./Game/Tests/Rendering/PlanarReflection.ST_PR04
			IEnumerable<Match> TestResults = Parser.GetAllMatches(@"LogAutomationController.+Test Completed. Result={(.+?)}\s+Name={(.+?)}\s+Path={(.+?)}");

			string[] AutomationChannel = Parser.GetLogChannel("AutomationController").ToArray();

			// Convert these lines into results by parsing out the details and then adding the events
			IEnumerable<AutomationTestResult> Results = TestStarts.Select(TestMatch =>
			{
				string DisplayName = TestMatch.Groups[1].ToString();
				string LongName = "";
				bool Completed = false;
				bool Passed = false;
				List<string> Events = new List<string>();
				
				Match ResultMatch = TestResults.Where(M => M.Groups[2].ToString() == DisplayName).FirstOrDefault();

				if (ResultMatch != null)
				{
					Completed = true;
					Passed = ResultMatch.Groups[1].ToString().ToLower() == "passed" ? true : false;
					LongName = ResultMatch.Groups[3].ToString();					

					string EventName = string.Format("BeginEvents: {0}", LongName);
					int EventIndex = Array.FindIndex(AutomationChannel, S => S.Contains(EventName)) + 1;

					if (EventIndex > 0)
					{
						while (EventIndex < AutomationChannel.Length)
						{
							string Event = AutomationChannel[EventIndex++];

							if (Event.Contains("EndEvents"))
							{
								break;
							}

							Events.Add(Event);
						}
					}
				}
				else
				{
					Events.Add(string.Format("Gauntlet.AutomationLogParser: Error: Test {0} incomplete.", DisplayName));
				}

				AutomationTestResult Result = new AutomationTestResult(DisplayName, LongName, Completed, Passed, Events);
				return Result;
			});

			return Results;
		}
	}
}