﻿namespace Satrabel.OpenContent.Components.Datasource.Search
{
    public class StringRuleValue : RuleValue
    {
        private string Value;
        public StringRuleValue(string value)
        {
            Value = value;
        }
        public override string AsString
        {
            get
            {
                return Value;
            }
        }
    }
}