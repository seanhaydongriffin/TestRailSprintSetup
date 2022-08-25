using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SharedProject;
using SharedProject.Models;
using System;
using System.Linq;
using System.Reflection;
using System.Xml;

namespace TestRailSprintSetup
{
    class Program
    {
        private static SharedProject.TestRail.APIClient TestRailClient = null;

        static void Main(string[] args)
        {
            Log.Initialise(System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "\\TestRailSprintSetup.log");
            Log.Initialise(null);
            AppConfig.Open();

            TestRailClient = new SharedProject.TestRail.APIClient(AppConfig.Get("TestRailUrl"));
            TestRailClient.User = AppConfig.Get("TestRailUser");
            TestRailClient.Password = AppConfig.Get("TestRailPassword");

            var Sprints = AppConfig.GetSectionGroup("Sprints").GetSection();

            var today = System.DateTime.Today;
            var unixTimestamp = (int)today.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds;
            Log.WriteLine("today = " + today);
            Log.WriteLine("unixTimestamp = " + unixTimestamp);

            var current_quarter = SharedProject.DateTime.GetNowQuarterInfo();
            Log.WriteLine("Current quarter is FY" + current_quarter.ShortYear + "Q" + current_quarter.Quarter);

            // The projects

            foreach (XmlNode TestProject in AppConfig.GetSectionGroup("TestRuns").GetSectionGroups())
            {
                var TestProjectId = TestProject.FirstChild.GetAttributeValue("Id");
                Log.WriteLine("TestProjectId = " + TestProjectId);

                // The milestones

                var milestones = (JObject)TestRailClient.SendGet("get_milestones/" + TestProjectId);
                var quarter_milestone = milestones.SelectToken("$..[?(@.name =~ /^FY" + current_quarter.ShortYear + "Q" + current_quarter.Quarter + " .*$/)]");

                if (quarter_milestone == null)
                {
                    Log.WriteLine("Not Found quarter milestone for this current quarter \"" + quarter_milestone + "\".  Will create one.");

                    // create the quarter milestone

                    var tr_milestone = new TestRailMilestone2();
                    var current_quarter_start_datetime = new System.DateTime(current_quarter.LongYear, 1, 1);
                    var current_quarter_end_datetime = new System.DateTime(current_quarter.LongYear, 1, System.DateTime.DaysInMonth(current_quarter.LongYear, 1));
                    var current_quarter_end_month = "";

                    if (current_quarter.Quarter == 1)
                    {
                        current_quarter_start_datetime = new System.DateTime(current_quarter.LongYear, 7, 1);
                        current_quarter_end_datetime = new System.DateTime(current_quarter.LongYear, 9, System.DateTime.DaysInMonth(current_quarter.LongYear, 9));
                        current_quarter_end_month = "Sep";
                    }

                    if (current_quarter.Quarter == 2)
                    {
                        current_quarter_start_datetime = new System.DateTime(current_quarter.LongYear, 10, 1);
                        current_quarter_end_datetime = new System.DateTime(current_quarter.LongYear, 12, System.DateTime.DaysInMonth(current_quarter.LongYear, 12));
                        current_quarter_end_month = "Dec";
                    }

                    if (current_quarter.Quarter == 3)
                    {
                        current_quarter_start_datetime = new System.DateTime(current_quarter.LongYear, 1, 1);
                        current_quarter_end_datetime = new System.DateTime(current_quarter.LongYear, 3, System.DateTime.DaysInMonth(current_quarter.LongYear, 3));
                        current_quarter_end_month = "Mar";
                    }

                    if (current_quarter.Quarter == 4)
                    {
                        current_quarter_start_datetime = new System.DateTime(current_quarter.LongYear, 4, 1);
                        current_quarter_end_datetime = new System.DateTime(current_quarter.LongYear, 6, System.DateTime.DaysInMonth(current_quarter.LongYear, 6));
                        current_quarter_end_month = "Jun";
                    }

                    tr_milestone.name = "FY" + current_quarter.ShortYear + "Q" + current_quarter.Quarter + " (ends " + current_quarter_end_month + ")";
                    tr_milestone.description = "";
                    tr_milestone.start_on = (int)current_quarter_start_datetime.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds;
                    tr_milestone.due_on = (int)current_quarter_end_datetime.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds;

                    Log.WriteLine("Posting new quarter milestone to endpoint \"add_milestone/" + TestProjectId + "\" the data ... " + tr_milestone);
                    quarter_milestone = (JToken)TestRailClient.SendPost("add_milestone/" + TestProjectId, tr_milestone);

                }

                var sprint_milestones = quarter_milestone["milestones"];
//                    Log.WriteLine("sprint_milestones = " + sprint_milestones);

                var sprint_milestone = sprint_milestones.SelectToken("$..[?(@.start_on <= " + unixTimestamp + " && @.due_on >= " + unixTimestamp + ")]");
//                    Log.WriteLine("sprint_milestone = " + sprint_milestone);

                if (sprint_milestone == null)
                {
                    Log.WriteLine("Not Found sprint milestone for this current sprint \"" + sprint_milestone + "\".  Will create one.");

                    // locate the new milestone from the sprint dates in the config

                    for (int sprint_num = 1; sprint_num <= 50; sprint_num++)
                    {
                        var Sprint = Sprints.Cast<XmlNode>().Where(n => n.Attributes["Name"].InnerText == "FY" + current_quarter.ShortYear + "Q" + current_quarter.Quarter + "S" + sprint_num).First();

                        if (Sprint == null)

                            break;

                        var SprintName = Sprint.GetAttributeValue("Name");
                        var SprintStart = Sprint.GetAttributeValue("Start");
                        var SprintEnd = Sprint.GetAttributeValue("End");
                        var sprint_start = System.DateTime.ParseExact(SprintStart, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                        var sprint_end = System.DateTime.ParseExact(SprintEnd, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);

                        if (today.Ticks >= sprint_start.Ticks && today.Ticks <= sprint_end.Ticks)
                        {
                            // create the milestone

                            var tr_milestone = new TestRailMilestone();
                            tr_milestone.name = "FY" + current_quarter.ShortYear + "Q" + current_quarter.Quarter + "S" + sprint_num + " (ends " + sprint_end.ToString("dd MMM") + ")";
                            tr_milestone.description = "";
                            tr_milestone.parent_id = (long)quarter_milestone["id"];
                            tr_milestone.start_on = (int)sprint_start.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds;
                            tr_milestone.due_on = (int)sprint_end.Subtract(new System.DateTime(1970, 1, 1)).TotalSeconds;

                            Log.WriteLine("Posting new sprint milestone to endpoint \"add_milestone/" + TestProjectId + "\" the data ... " + tr_milestone);
                            sprint_milestone = (JToken)TestRailClient.SendPost("add_milestone/" + TestProjectId, tr_milestone);
                            break;
                        }
                    }
                }

                var plans = (JObject)TestRailClient.SendGet("get_plans/" + TestProjectId + "&is_completed=0&milestone_id=" + sprint_milestone["id"]);

                // The test plans

                foreach (XmlNode TestPlan in TestProject.SelectNodes("*[starts-with(name(), 'TestPlan_')]"))
                {
                    var TestPlanNameSuffix = TestPlan.FirstChild.GetAttributeValue("NameSuffix");
                    var plan_name = sprint_milestone["name"].ToString().Split(' ').FirstOrDefault() + " " + TestPlanNameSuffix;
                    var plan_id = plans.SelectToken("$..[?(@.name == '" + plan_name + "')].id");

                    // if the test plan is not in TestRail

                    if (plan_id == null)
                    {
                        Log.WriteLine("Not Found Project \"" + TestProjectId + "\", Milestone \"" + sprint_milestone["name"] + "\" and plan with name \"" + plan_name + "\".");
                        Log.WriteLine("Creating this plan...");

                        var CopyFromNameSuffix = TestPlan.FirstChild.GetAttributeValue("CopyFromNameSuffix");
                        XmlNode CopyFromTestPlan = null;

                        if (CopyFromNameSuffix == null)

                            CreateTestRailPlan(TestProjectId, plan_name, (long)sprint_milestone["id"], TestPlan);
                        else
                        {
                            CopyFromTestPlan = TestProject.SelectSingleNode("*[starts-with(name(), 'TestPlan_')]/add[@NameSuffix='" + CopyFromNameSuffix + "']/..");
                            CreateTestRailPlan(TestProjectId, plan_name, (long)sprint_milestone["id"], CopyFromTestPlan);
                        }

                    }
                }
            }
        }



        private static void CreateTestRailPlan(string TestProjectId, string plan_name, long sprint_milestone_id, XmlNode TestPlan)
        {


            var tr_plan = new TestRailPlan3();
            tr_plan.name = plan_name;
            tr_plan.description = "";
            tr_plan.milestone_id = sprint_milestone_id;

            foreach (XmlNode TestSuite in TestPlan.SelectNodes("*[starts-with(name(), 'TestSuite_')]"))
            {
                var TestSuiteId = TestSuite.FirstChild.GetAttributeValue("Id");

                var tr_plan_entry = new TestRailPlanEntry();
                tr_plan_entry.suite_id = long.Parse(TestSuiteId);
                //tr_plan.assignedto_id = 1;
                tr_plan_entry.include_all = true;

                foreach (XmlNode TestConfiguration in TestSuite.SelectSingleNode("TestConfiguration").SelectNodes("*"))
                {
                    var TestConfigurationId = TestConfiguration.GetAttributeValue("Id");
                    tr_plan_entry.config_ids.Add(long.Parse(TestConfigurationId));
                }

                foreach (XmlNode TestRun in TestSuite.SelectNodes("*[starts-with(name(), 'TestRun_')]"))
                {
                    var tr_run = new TestRailRun2();
                    tr_run.include_all = false;

                    foreach (XmlNode TestCase in TestRun.SelectSingleNode("TestCase").SelectNodes("*"))
                    {
                        var TestCaseId = TestCase.GetAttributeValue("Id");
                        tr_run.case_ids.Add(long.Parse(TestCaseId));
                    }

                    foreach (XmlNode TestConfiguration in TestRun.SelectSingleNode("TestConfiguration").SelectNodes("*"))
                    {
                        var TestConfigurationId = TestConfiguration.GetAttributeValue("Id");
                        tr_run.config_ids.Add(long.Parse(TestConfigurationId));
                    }

                    tr_plan_entry.runs.Add(tr_run);
                }

                tr_plan.entries.Add(tr_plan_entry);
            }

            // create the test plan

            Log.WriteLine("Posting new plan to endpoint \"add_plan/" + TestProjectId + "\" with data ... " + tr_plan);
            var plan = (JObject)TestRailClient.SendPost("add_plan/" + TestProjectId, tr_plan);

            var entries = plan["entries"];

            // add the plan and run(s) into the associated execution group(s) in CoPilot

            foreach (XmlNode TestSuite in TestPlan.SelectNodes("*[starts-with(name(), 'TestSuite_')]"))
            {
                var TestSuiteId = TestSuite.FirstChild.GetAttributeValue("Id");
                int run_num = 1;

                foreach (XmlNode TestRun in TestSuite.SelectNodes("*[starts-with(name(), 'TestRun_')]"))
                {
                    foreach (XmlNode ExecutionGroup in TestRun.SelectSingleNode("ExecutionGroup").SelectNodes("*"))
                    {
                        var ExecutionGroupId = ExecutionGroup.GetAttributeValue("Id");
                        var ExecutionGroupSchemaName = ExecutionGroup.GetAttributeValue("SchemaName");

                        var runs = entries.SelectToken("$[?(@.suite_id == " + TestSuiteId + ")].runs");

                        Log.WriteLine("Connecting to CoPilot DB schema \"" + ExecutionGroupSchemaName + "\"");
                        var dbConnect = new DBConnect(ExecutionGroupSchemaName);
                        var sql = "update execution_group set external_plan_id = " + plan["id"] + ", external_plan_name = \"" + plan["name"] + "\", external_exe_rec_run_id = " + runs[run_num - 1]["id"] + ", external_exe_rec_run_name = \"" + runs[run_num - 1]["name"] + "\" where id = " + ExecutionGroupId + ";";
                        Log.WriteLine("Executing SQL \"" + sql + "\"");
                        var result_arr = dbConnect.Select<CoPilotExecutionGroup3>(sql);
                    }

                    run_num++;
                }
            }


        }



    }
}
