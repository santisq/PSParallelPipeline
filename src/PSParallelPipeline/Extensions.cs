using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Text;

namespace PSParallelPipeline;

internal static class Extensions
{
    internal static InitialSessionState AddFunctions(
        this InitialSessionState initialSessionState,
        string[]? functionsToAdd,
        PSCmdlet cmdlet)
    {
        if (functionsToAdd is not null)
        {
            foreach (string function in functionsToAdd)
            {
                CommandInfo? commandInfo = cmdlet
                    .InvokeCommand
                    .GetCommand(function, CommandTypes.Function);

                if (commandInfo is null)
                {
                    continue;
                }

                initialSessionState.Commands.Add(new SessionStateFunctionEntry(
                    name: function,
                    definition: commandInfo.Definition));
            }
        }

        return initialSessionState;
    }

    internal static InitialSessionState AddVariables(
        this InitialSessionState initialSessionState,
        Hashtable? variables,
        PSCmdlet cmdlet)
    {
        if (variables is not null)
        {
            foreach (DictionaryEntry pair in variables)
            {
                cmdlet.ThrowIfVariableIsScriptBlock(pair.Value);
                initialSessionState.Variables.Add(new SessionStateVariableEntry(
                    name: LanguagePrimitives.ConvertTo<string>(pair.Key),
                    value: pair.Value,
                    description: null));
            }
        }

        return initialSessionState;
    }

    internal static Dictionary<string, object?> GetUsingParameters(
        this ScriptBlock script,
        PSCmdlet cmdlet)
    {
        Dictionary<string, object?> usingParams = [];
        IEnumerable<UsingExpressionAst> usingExpressionAsts = script.Ast
            .FindAll((a) => a is UsingExpressionAst, true)
            .Cast<UsingExpressionAst>();

        foreach (UsingExpressionAst usingStatement in usingExpressionAsts)
        {
            VariableExpressionAst backingVariableAst = UsingExpressionAst
                .ExtractUsingVariable(usingStatement);

            string varPath = backingVariableAst.VariablePath.UserPath;

            string varText = usingStatement.ToString();
            if (usingStatement.SubExpression is VariableExpressionAst)
            {
                varText = varText.ToLowerInvariant();
            }

            string key = Convert.ToBase64String(Encoding.Unicode.GetBytes(varText));
            object? value = cmdlet.GetVariableValue(varPath);
            cmdlet.ThrowIfUsingValueIsScriptBlock(value);

            if (usingParams.ContainsKey(key))
            {
                continue;
            }

            if (usingStatement.SubExpression is MemberExpressionAst or IndexExpressionAst)
            {
                value = ExtractUsingExpressionValue(value, usingStatement.SubExpression);
                cmdlet.ThrowIfUsingValueIsScriptBlock(value);
            }

            usingParams.Add(key, value);
        }

        return usingParams;
    }

    private static object? ExtractUsingExpressionValue(
        object? value,
        ExpressionAst ast)
    {
        VariableExpressionAst usingVariable = (VariableExpressionAst)ast
            .Find(a => a is VariableExpressionAst, false);

        ExpressionAst lookupAst = new ConstantExpressionAst(ast.Extent, value);
        Ast? currentAst = usingVariable;

        while ((currentAst = currentAst?.Parent) is not null)
        {
            switch (currentAst)
            {
                case IndexExpressionAst indexAst:
                    lookupAst = new IndexExpressionAst(
                        extent: indexAst.Extent,
                        target: lookupAst,
                        index: (ExpressionAst)indexAst.Index.Copy());
                    currentAst = indexAst;
                    break;

                case MemberExpressionAst memberAst:
                    lookupAst = new MemberExpressionAst(
                        extent: memberAst.Extent,
                        expression: lookupAst,
                        member: (ExpressionAst)memberAst.Member.Copy(),
                        memberAst.Static);
                    currentAst = memberAst;
                    break;

                default:
                    goto CreateAst;
            }
        }

    CreateAst:
        ScriptBlockAst extractionAst = new(
            extent: ast.Extent,
            paramBlock: null,
            statements: new StatementBlockAst(
                extent: ast.Extent,
                statements: [ new PipelineAst(
                    extent: ast.Extent,
                    pipelineElements: [ new CommandExpressionAst(
                        extent: ast.Extent,
                        expression: lookupAst,
                        redirections: null)
                    ])
                ],
                traps: null),
            isFilter: false);

        return extractionAst
            .GetScriptBlock()
            .InvokeReturnAsIs();
    }
}
