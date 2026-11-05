using System;
using System.Collections.Generic;
using Poe2FilterEditor.Infrastructure;
using Poe2FilterEditor.Models;

namespace Poe2FilterEditor.ViewModels
{
    /// <summary>数值条件（物品等级、掉落等级…）。</summary>
    public sealed class NumericConditionViewModel : ObservableObject
    {
        private readonly RuleViewModel _owner;
        private bool _isEnabled;
        private string _operator = ">=";
        private string _valueText = string.Empty;
        private bool _hasError;

        public NumericConditionViewModel(RuleViewModel owner, string keyword, string chineseName)
        {
            _owner = owner;
            Keyword = keyword;
            ChineseName = chineseName;
        }

        public string Keyword { get; }

        public string ChineseName { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                {
                    _owner.ApplyNumeric(this);
                }
            }
        }

        public string Operator
        {
            get => _operator;
            set
            {
                if (SetProperty(ref _operator, value))
                {
                    _owner.ApplyNumeric(this);
                }
            }
        }

        public string ValueText
        {
            get => _valueText;
            set
            {
                if (SetProperty(ref _valueText, value))
                {
                    _owner.ApplyNumeric(this);
                }
            }
        }

        /// <summary>输入不是有效整数时由界面标红提示。</summary>
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public IReadOnlyList<NamedChoice> OperatorChoices => NamedChoice.Operators;
    }

    /// <summary>布尔条件（已腐化、已鉴定…），三态：不限 / 是 / 否。</summary>
    public sealed class BooleanConditionViewModel : ObservableObject
    {
        private readonly RuleViewModel _owner;
        private string _value = string.Empty;

        public BooleanConditionViewModel(RuleViewModel owner, string keyword, string chineseName)
        {
            _owner = owner;
            Keyword = keyword;
            ChineseName = chineseName;
        }

        public string Keyword { get; }

        public string ChineseName { get; }

        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value ?? string.Empty))
                {
                    _owner.ApplyBoolean(this);
                }
            }
        }

        public IReadOnlyList<NamedChoice> Choices => NamedChoice.Booleans;
    }
}
