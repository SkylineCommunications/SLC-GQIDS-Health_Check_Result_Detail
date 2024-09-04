/*
****************************************************************************
*  Copyright (c) 2024,  Skyline Communications NV  All Rights Reserved.    *
****************************************************************************

By using this script, you expressly agree with the usage terms and
conditions set out below.
This script and all related materials are protected by copyrights and
other intellectual property rights that exclusively belong
to Skyline Communications.

A user license granted for this script is strictly for personal use only.
This script may not be used in any way by anyone without the prior
written consent of Skyline Communications. Any sublicensing of this
script is forbidden.

Any modifications to this script by the user are only allowed for
personal use and within the intended purpose of the script,
and will remain the sole responsibility of the user.
Skyline Communications will not be responsible for any damages or
malfunctions whatsoever of the script resulting from a modification
or adaptation by the user.

The content of this script is confidential information.
The user hereby agrees to keep this confidential information strictly
secret and confidential and not to disclose or reveal it, in whole
or in part, directly or indirectly to any person, entity, organization
or administration without the prior written consent of
Skyline Communications.

Any inquiries can be addressed to:

	Skyline Communications NV
	Ambachtenstraat 33
	B-8870 Izegem
	Belgium
	Tel.	: +32 51 31 35 69
	Fax.	: +32 51 31 01 29
	E-mail	: info@skyline.be
	Web		: www.skyline.be
	Contact	: Ben Vandenberghe

****************************************************************************
Revision History:

DATE		VERSION		AUTHOR			COMMENTS

03/09/2024	1.0.0.1		DPR, Skyline	Initial version
****************************************************************************
*/

namespace HealthCheckResultDetail_1
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text.RegularExpressions;

	using Skyline.DataMiner.Analytics.GenericInterface;
	using Skyline.DataMiner.Net.Messages;

	[GQIMetaData(Name = "Health_Check_Result_Detail")]

	public class HealthCheckResultDetail : IGQIDataSource, IGQIInputArguments, IGQIOnInit
	{
		private readonly GQIStringArgument _index = new GQIStringArgument("Index") { IsRequired = false };

		private readonly Dictionary<string, string> operatorSymbols = new Dictionary<string, string>
		{
			{ "EqualTo", "=" },
			{ "NotEqualTo", "≠" },
			{ "LessThan", "<" },
			{ "LessThanOrEqual", "≤" },
			{ "GreaterThan", ">" },
			{ "GreaterThanOrEqual", "≥" },
			{ "Between", "Between" },
			{ "NotBetween", "Not Between" },
		};

		private string _indexValue;

		private GQIDMS _dms;

		public GQIColumn[] GetColumns()
		{
			return new GQIColumn[]
			{
				new GQIStringColumn("DMA"),
				new GQIStringColumn("Index"),
				new GQIStringColumn("Passing Condition"),
				new GQIStringColumn("Retrieved Value"),
			};
		}

		public GQIArgument[] GetInputArguments()
		{
			return new GQIArgument[] { _index };
		}

		public OnArgumentsProcessedOutputArgs OnArgumentsProcessed(OnArgumentsProcessedInputArgs args)
		{
			_indexValue = args.GetArgumentValue(_index);
			return default;
		}

		public GQIPage GetNextPage(GetNextPageInputArgs args)
		{
			return GetData();
		}

		public OnInitOutputArgs OnInit(OnInitInputArgs args)
		{
			_dms = args.DMS;
			return default;
		}

		private static string CreateColumnFilter(params int[] columnPIDs)
		{
			return $"columns={string.Join(",", columnPIDs)}";
		}

		private GQIPage GetData()
		{
			if (String.IsNullOrEmpty(_indexValue))
			{
				return new GQIPage(new List<GQIRow>().ToArray()) { HasNextPage = false };
			}

			var elements = GetElements().ToList();

			if (elements.Count != 1)
			{
				return new GQIPage(new List<GQIRow>().ToArray()) { HasNextPage = false };
			}

			var filters = new[] { "ForceFullTable=true", CreateColumnFilter(2005), $"value=2001 == {_indexValue}" };

			var resultTable = GetTablePage(elements[0].DataMinerID, elements[0].ElementID, 2000, filters)?.NewValue;

			if (resultTable == null)
			{
				return new GQIPage(new List<GQIRow>().ToArray()) { HasNextPage = false };
			}

			var columns = resultTable.ArrayValue;

			if (columns.Length != 2)
			{
				return new GQIPage(new List<GQIRow>().ToArray()) { HasNextPage = false };
			}

			ParameterValue[] testId = columns[0].ArrayValue;
			ParameterValue[] testDetails = columns[1].ArrayValue;

			if (testId.Length != 1)
			{
				return new GQIPage(new List<GQIRow>().ToArray()) { HasNextPage = false };
			}

			string testDetailsValue = testDetails[0].CellValue.StringValue;

			var reasonLines = testDetailsValue.Split(new[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries);

			if (reasonLines.Length < 2)
			{
				return new GQIPage(new List<GQIRow>().ToArray()) { HasNextPage = false };
			}

			List<GQIRow> rows = ProcessRows(reasonLines);
			return new GQIPage(rows.ToArray()) { HasNextPage = false };
		}

		private List<GQIRow> ProcessRows(string[] reasonLines)
		{
			string dmaNamePattern = @"Test case for ([^,]+)";
			string indexPattern = @"Index\s""([^""]+)""";
			string operatorPattern = @"Operator:\s(\w+)";

			string thresholdPattern = @"Threshold:\s(.*?),\sOperator";
			string actualPattern = @"Actual:\s(.*)";

			var rows = new List<GQIRow>();

			for (int i = 1; i < reasonLines.Length; i++)
			{
				// Extracting the test case
				var dmaNameMatch = Regex.Match(reasonLines[i], dmaNamePattern);
				string dmaName = dmaNameMatch.Groups[1].Value;

				// Extracting the index (if it exists)
				var indexMatch = Regex.Match(reasonLines[i], indexPattern);
				string index = indexMatch.Success ? indexMatch.Groups[1].Value : "N/A";

				// Extracting the threshold
				var thresholdMatch = Regex.Match(reasonLines[i], thresholdPattern);
				string threshold = thresholdMatch.Groups[1].Value;

				// Extracting the operator
				var operatorMatch = Regex.Match(reasonLines[i], operatorPattern);
				string operatorValue = operatorMatch.Groups[1].Value;
				operatorValue = operatorSymbols.ContainsKey(operatorValue) ? operatorSymbols[operatorValue] : operatorValue;

				// Extracting the actual value
				var actualMatch = Regex.Match(reasonLines[i], actualPattern);
				string actual = actualMatch.Groups[1].Value;

				var cells = new List<GQICell>
				{
					new GQICell { Value = dmaName},
					new GQICell { Value = index},
					new GQICell { Value = $"{operatorValue} {threshold}" },
					new GQICell { Value = actual },
				};

				var rowData = new GQIRow(cells.ToArray());
				rows.Add(rowData);
			}

			return rows;
		}

		private ParameterChangeEventMessage GetTablePage(int dmaID, int elementID, int parameterID, string[] filters)
		{
			var request = new GetPartialTableMessage(dmaID, elementID, parameterID, filters);
			try
			{
				return _dms.SendMessage(request) as ParameterChangeEventMessage;
			}
			catch (Exception)
			{
				return default;
			}
		}

		private IEnumerable<LiteElementInfoEvent> GetElements()
		{
			var request = new GetLiteElementInfo(includeStopped: false)
			{
				ProtocolName = "Skyline Health Check Manager",
				ProtocolVersion = "Production",
			};

			try
			{
				return _dms.SendMessages(request).OfType<LiteElementInfoEvent>();
			}
			catch (Exception)
			{
				return Array.Empty<LiteElementInfoEvent>();
			}
		}
	}
}
