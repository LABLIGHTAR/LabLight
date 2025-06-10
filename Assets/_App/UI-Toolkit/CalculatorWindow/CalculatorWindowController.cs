using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using NCalc;
using System.Collections.Generic;

public class CalculatorWindowController : BaseWindowController
{
    private Label _displayLabel;
    private Button _modeButton;
    private IAudioService _audioService;

    // State
    private string _currentExpression = "";
    private bool _isErrorState = false;
    private bool _isDegrees = true;
    private double _memory = 0;

    protected override void OnEnable()
    {
        base.OnEnable();
        _audioService = ServiceRegistry.GetService<IAudioService>();
        
        _displayLabel = rootVisualElement.Q<Label>("display-label");
        _modeButton = rootVisualElement.Q<Button>("mode-button");
        
        if (_displayLabel == null || _modeButton == null)
        {
            Debug.LogError("CalculatorWindowController: A required UI element was not found.");
            return;
        }

        RegisterButtonCallbacks();
        UpdateDisplay();
    }

    private void RegisterButtonCallbacks()
    {
        // Numbers
        for (int i = 0; i <= 9; i++)
        {
            int num = i; // Local copy for the closure
            rootVisualElement.Q<Button>($"button-{i}")?.RegisterCallback<ClickEvent>(evt => OnNumberPressed(num.ToString(), evt));
        }

        // Operators
        rootVisualElement.Q<Button>("add-button")?.RegisterCallback<ClickEvent>(evt => OnOperatorPressed("+", evt));
        rootVisualElement.Q<Button>("subtract-button")?.RegisterCallback<ClickEvent>(evt => OnOperatorPressed("-", evt));
        rootVisualElement.Q<Button>("multiply-button")?.RegisterCallback<ClickEvent>(evt => OnOperatorPressed("*", evt));
        rootVisualElement.Q<Button>("divide-button")?.RegisterCallback<ClickEvent>(evt => OnOperatorPressed("/", evt));
        rootVisualElement.Q<Button>("decimal-button")?.RegisterCallback<ClickEvent>(OnDecimalPressed);
        rootVisualElement.Q<Button>("power-button")?.RegisterCallback<ClickEvent>(OnPowerPressed);
        rootVisualElement.Q<Button>("left-paren-button")?.RegisterCallback<ClickEvent>(evt => OnTextPressed("(", evt));
        rootVisualElement.Q<Button>("right-paren-button")?.RegisterCallback<ClickEvent>(evt => OnTextPressed(")", evt));

        // Utility
        rootVisualElement.Q<Button>("clear-button")?.RegisterCallback<ClickEvent>(OnClearPressed);
        rootVisualElement.Q<Button>("clear-entry-button")?.RegisterCallback<ClickEvent>(OnClearEntryPressed);
        rootVisualElement.Q<Button>("equals-button")?.RegisterCallback<ClickEvent>(OnEqualsPressed);
        rootVisualElement.Q<Button>("negate-button")?.RegisterCallback<ClickEvent>(OnNegatePressed);
        _modeButton.RegisterCallback<ClickEvent>(OnModeToggled);

        // Scientific Functions
        rootVisualElement.Q<Button>("sin-button")?.RegisterCallback<ClickEvent>(evt => OnFunctionPressed("Sin", evt));
        rootVisualElement.Q<Button>("cos-button")?.RegisterCallback<ClickEvent>(evt => OnFunctionPressed("Cos", evt));
        rootVisualElement.Q<Button>("tan-button")?.RegisterCallback<ClickEvent>(evt => OnFunctionPressed("Tan", evt));
        rootVisualElement.Q<Button>("asin-button")?.RegisterCallback<ClickEvent>(evt => OnFunctionPressed("Asin", evt));
        rootVisualElement.Q<Button>("acos-button")?.RegisterCallback<ClickEvent>(evt => OnFunctionPressed("Acos", evt));
        rootVisualElement.Q<Button>("atan-button")?.RegisterCallback<ClickEvent>(evt => OnFunctionPressed("Atan", evt));
        rootVisualElement.Q<Button>("log-button")?.RegisterCallback<ClickEvent>(evt => OnFunctionPressed("Log10", evt));
        rootVisualElement.Q<Button>("ln-button")?.RegisterCallback<ClickEvent>(evt => OnFunctionPressed("Log", evt));
        rootVisualElement.Q<Button>("sqrt-button")?.RegisterCallback<ClickEvent>(evt => OnFunctionPressed("Sqrt", evt));
        rootVisualElement.Q<Button>("sqr-button")?.RegisterCallback<ClickEvent>(OnSquarePressed);
        rootVisualElement.Q<Button>("factorial-button")?.RegisterCallback<ClickEvent>(OnFactorialPressed);
        
        // Constants
        rootVisualElement.Q<Button>("pi-button")?.RegisterCallback<ClickEvent>(evt => OnNumberPressed("PI", evt));
        rootVisualElement.Q<Button>("e-button")?.RegisterCallback<ClickEvent>(evt => OnNumberPressed("E", evt));

        // Memory
        rootVisualElement.Q<Button>("mem-clear-button")?.RegisterCallback<ClickEvent>(OnMemoryClear);
        rootVisualElement.Q<Button>("mem-recall-button")?.RegisterCallback<ClickEvent>(OnMemoryRecall);
        rootVisualElement.Q<Button>("mem-add-button")?.RegisterCallback<ClickEvent>(OnMemoryAdd);
    }

    private void PlayClickSound(ClickEvent evt) => _audioService?.PlayButtonPress((evt.currentTarget as VisualElement).worldBound.center);

    private string[] GetTokens() => Regex.Split(_currentExpression.Trim(), @"\s+");

    private void OnNumberPressed(string number, ClickEvent evt)
    {
        PlayClickSound(evt);
        if (_isErrorState) ClearAll();
        
        var lastToken = GetTokens().LastOrDefault();
        if (lastToken != null && (lastToken.Equals("PI", StringComparison.OrdinalIgnoreCase) || lastToken.Equals("E", StringComparison.OrdinalIgnoreCase)))
        {
            _currentExpression += " * ";
        }

        _currentExpression += number;
        UpdateDisplay();
    }
    
    private void OnTextPressed(string text, ClickEvent evt)
    {
        PlayClickSound(evt);
        if (_isErrorState) ClearAll();
        _currentExpression += text;
        UpdateDisplay();
    }

    private void OnDecimalPressed(ClickEvent evt)
    {
        PlayClickSound(evt);
        if (_isErrorState) ClearAll();

        var tokens = GetTokens();
        var lastToken = tokens.LastOrDefault() ?? "";
        if (!lastToken.Contains("."))
        {
            _currentExpression += ".";
        }
        UpdateDisplay();
    }

    private void OnOperatorPressed(string op, ClickEvent evt)
    {
        PlayClickSound(evt);
        if (_isErrorState) ClearAll();

        _currentExpression = _currentExpression.Trim();
        char lastChar = _currentExpression.LastOrDefault();
        if ("+-*/^".Contains(lastChar))
        {
             _currentExpression = _currentExpression.Substring(0, _currentExpression.Length - 1);
        }

        _currentExpression += $" {op} ";
        UpdateDisplay();
    }

    private void OnFunctionPressed(string func, ClickEvent evt)
    {
        PlayClickSound(evt);
        if (_isErrorState) ClearAll();
        _currentExpression += $"{func}(";
        UpdateDisplay();
    }

    private void OnPowerPressed(ClickEvent evt)
    {
        PlayClickSound(evt);
        if (_isErrorState) ClearAll();
        string lastOperand = ExtractLastOperand(ref _currentExpression);
        if (!string.IsNullOrEmpty(lastOperand))
        {
            _currentExpression += $" Pow({lastOperand}, ";
        }
        UpdateDisplay();
    }

    private void OnSquarePressed(ClickEvent evt)
    {
        PlayClickSound(evt);
        if (_isErrorState) ClearAll();
        string lastOperand = ExtractLastOperand(ref _currentExpression);
        if (!string.IsNullOrEmpty(lastOperand))
        {
            _currentExpression += $" Pow({lastOperand}, 2) ";
        }
        UpdateDisplay();
    }

    private void OnNegatePressed(ClickEvent evt)
    {
        PlayClickSound(evt);
        if (_isErrorState) ClearAll();

        var tokens = GetTokens();
        if (tokens.Length == 0) return;

        var lastToken = tokens.Last();
        if (double.TryParse(lastToken, out _))
        {
            var newTokens = tokens.Take(tokens.Length - 1).ToList();
            newTokens.Add($"(-{lastToken})");
            _currentExpression = string.Join(" ", newTokens);
        }
        UpdateDisplay();
    }

    private void OnFactorialPressed(ClickEvent evt)
    {
        PlayClickSound(evt);
        if (_isErrorState) ClearAll();

        var tokens = GetTokens();
        if (tokens.Length == 0) return;

        var lastToken = tokens.Last();
        if (double.TryParse(lastToken, out _))
        {
            var newTokens = tokens.Take(tokens.Length - 1).ToList();
            newTokens.Add($"Factorial({lastToken})");
            _currentExpression = string.Join(" ", newTokens);
        }
        UpdateDisplay();
    }
    
    private void OnModeToggled(ClickEvent evt)
    {
        PlayClickSound(evt);
        _isDegrees = !_isDegrees;
        _modeButton.text = _isDegrees ? "Deg" : "Rad";
    }

    private void OnMemoryClear(ClickEvent evt)
    {
        PlayClickSound(evt);
        _memory = 0;
    }

    private void OnMemoryRecall(ClickEvent evt)
    {
        PlayClickSound(evt);
        _currentExpression += _memory.ToString(CultureInfo.InvariantCulture);
        UpdateDisplay();
    }

    private void OnMemoryAdd(ClickEvent evt)
    {
        PlayClickSound(evt);
        if (_isErrorState || string.IsNullOrWhiteSpace(_currentExpression)) return;
        try
        {
            var result = EvaluateExpression(_currentExpression);
            _memory += result;
        }
        catch (Exception e)
        {
            SetErrorState(e.Message);
        }
    }

    private void OnClearPressed(ClickEvent evt)
    {
        PlayClickSound(evt);
        ClearAll();
    }

    private void OnClearEntryPressed(ClickEvent evt)
    {
        PlayClickSound(evt);
        if (_isErrorState)
        {
            ClearAll();
            return;
        }
        
        var tokens = GetTokens();
        if (tokens.Length > 0)
        {
            _currentExpression = string.Join(" ", tokens.Take(tokens.Length - 1)) + " ";
        }
        
        UpdateDisplay();
    }

    private void OnEqualsPressed(ClickEvent evt)
    {
        PlayClickSound(evt);
        if (_isErrorState || string.IsNullOrWhiteSpace(_currentExpression)) return;

        try
        {
            var result = EvaluateExpression(_currentExpression);
            _currentExpression = result.ToString(CultureInfo.InvariantCulture);
        }
        catch (Exception e)
        {
            SetErrorState(e.Message);
        }
        UpdateDisplay();
    }
    
    private double EvaluateExpression(string expressionStr)
    {
        string finalExpression = expressionStr.Replace(")(", ")*(");
        Expression e = new Expression(finalExpression, EvaluateOptions.IgnoreCase);

        e.Parameters["PI"] = Math.PI;
        e.Parameters["E"] = Math.E;

        double angleMultiplier = _isDegrees ? Math.PI / 180.0 : 1.0;
        e.EvaluateFunction += (name, args) =>
        {
            // Only handle specific functions we need to override. Let NCalc handle the rest (like Pow for exponents).
            var funcName = name.ToUpperInvariant();
            var singleArgFuncs = new[] { "SIN", "COS", "TAN", "ASIN", "ACOS", "ATAN", "FACTORIAL", "LOG", "LOG10", "SQRT" };

            if (!singleArgFuncs.Contains(funcName))
            {
                return;
            }

            if (args.EvaluateParameters().Length == 1)
            {
                double val = Convert.ToDouble(args.Parameters[0].Evaluate());
                switch (funcName)
                {
                    case "SIN": args.Result = Math.Sin(val * angleMultiplier); break;
                    case "COS": args.Result = Math.Cos(val * angleMultiplier); break;
                    case "TAN": args.Result = Math.Tan(val * angleMultiplier); break;
                    case "ASIN": args.Result = Math.Asin(val) / angleMultiplier; break;
                    case "ACOS": args.Result = Math.Acos(val) / angleMultiplier; break;
                    case "ATAN": args.Result = Math.Atan(val) / angleMultiplier; break;
                    case "FACTORIAL": args.Result = Factorial((int)val); break;
                    case "LOG": args.Result = Math.Log(val); break;
                    case "LOG10": args.Result = Math.Log10(val); break;
                    case "SQRT": args.Result = Math.Sqrt(val); break;
                }
            }
        };

        var result = e.Evaluate();
        return Convert.ToDouble(result);
    }

    private double Factorial(int n)
    {
        if (n < 0 || n > 20) return double.NaN;
        if (n == 0) return 1;
        double result = 1;
        for (int i = 1; i <= n; i++)
        {
            result *= i;
        }
        return result;
    }

    private string ExtractLastOperand(ref string expression)
    {
        expression = expression.Trim();
        if (string.IsNullOrEmpty(expression)) return "";

        var tokens = new List<string>(GetTokens());
        if (tokens.Count == 0) return "";

        string operand;

        if (tokens.Last() == ")")
        {
            int parenBalance = 0;
            int startIndex = -1;
            for (int i = tokens.Count - 1; i >= 0; i--)
            {
                if (tokens[i] == ")") parenBalance++;
                if (tokens[i] == "(") parenBalance--;
                if (parenBalance == 0)
                {
                    startIndex = i;
                    break;
                }
            }

            if (startIndex != -1)
            {
                var operandTokens = tokens.GetRange(startIndex, tokens.Count - startIndex);
                operand = string.Join(" ", operandTokens);
                tokens.RemoveRange(startIndex, tokens.Count - startIndex);
                expression = string.Join(" ", tokens);
                return operand;
            }
        }
        
        operand = tokens.Last();
        tokens.RemoveAt(tokens.Count - 1);
        expression = string.Join(" ", tokens);
        return operand;
    }

    private void SetErrorState(string errorMessage)
    {
        _currentExpression = "Error";
        _isErrorState = true;
        Debug.LogError($"Calculator Error: {errorMessage} for expression '{_currentExpression}'");
    }

    private void UpdateDisplay()
    {
        if (_displayLabel != null)
        {
            _displayLabel.text = string.IsNullOrWhiteSpace(_currentExpression) ? "0" : _currentExpression.Replace(" ", "");
        }
    }

    private void ClearAll()
    {
        _currentExpression = "";
        _isErrorState = false;
        UpdateDisplay();
    }
}