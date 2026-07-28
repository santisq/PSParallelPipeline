using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PSParallelPipeline;

internal static class MiscExtensions
{
    extension(InitialSessionState initialSessionState)
    {
        internal InitialSessionState AddFunctions(
            string[]? functionsToAdd,
            PSCmdlet cmdlet)
        {
            if (functionsToAdd is not null)
            {
                foreach (string function in functionsToAdd)
                {
                    IEnumerable<CommandInfo> commands = cmdlet
                        .InvokeCommand
                        .GetCommands(
                            name: function,
                            commandTypes: CommandTypes.Function,
                            nameIsPattern: WildcardPattern.ContainsWildcardCharacters(function));

                    bool addedOne = false;
                    foreach (CommandInfo command in commands)
                    {
                        addedOne = true;
                        initialSessionState.Commands.Add(
                            new SessionStateFunctionEntry(
                                name: command.Name,
                                definition: command.Definition));
                    }

                    if (!addedOne)
                        cmdlet.ThrowFunctionNotFoundError(function);
                }
            }

            return initialSessionState;
        }

        internal InitialSessionState AddVariables(
            IDictionary[]? variables,
            PSCmdlet cmdlet)
        {
            if (variables is not null)
            {
                foreach (IDictionary dict in variables)
                {
                    foreach (DictionaryEntry pair in dict)
                    {
                        cmdlet.ThrowIfVariableIsScriptBlock(pair.Value);
                        initialSessionState.Variables.Add(new SessionStateVariableEntry(
                            name: LanguagePrimitives.ConvertTo<string>(pair.Key),
                            value: pair.Value,
                            description: null));
                    }
                }
            }

            return initialSessionState;
        }

        internal InitialSessionState ImportModules(
            string[]? modulesToImport)
        {
            if (modulesToImport is not null)
                initialSessionState.ImportPSModule(modulesToImport);

            return initialSessionState;
        }

        internal InitialSessionState ImportModulesFromPath(
            string[]? modulePaths,
            PSCmdlet cmdlet)
        {

            if (modulePaths is not null)
            {
                foreach (string path in modulePaths)
                {
                    string resolved = cmdlet.ResolvePath(path);
                    initialSessionState.ImportPSModulesFromPath(resolved);
                }
            }

            return initialSessionState;
        }
    }

    extension(PSCmdlet cmdlet)
    {
        private string ResolvePath(string path)
        {
            string resolved = cmdlet.SessionState.Path.GetUnresolvedProviderPathFromPSPath(
                path: path,
                provider: out ProviderInfo provider,
                drive: out _);

            provider.ThrowIfInvalidProvider(path, cmdlet);
            resolved.ThrowIfNotDirectory(cmdlet);
            return resolved.TrimEnd('\\', '/');
        }
    }

    extension(ScriptBlock script)
    {
        private static object? ExtractUsingExpressionValue(object? value, ExpressionAst ast)
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
                    statements: [
                        new PipelineAst(
                        extent: ast.Extent,
                        pipelineElements: [
                            new CommandExpressionAst(
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

        internal Dictionary<string, object?> GetUsingParameters(PSCmdlet cmdlet)
        {
            Dictionary<string, object?> usingParams = [];
            IEnumerable<UsingExpressionAst> usingExpressionAsts = script.Ast
                .FindAll(a => a is UsingExpressionAst, true)
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
    }



    extension(PowerShell powershell)
    {
        internal Task InvokeAsync<TOut>(PSDataCollection<TOut> output)
            => Task.Factory.FromAsync(
                powershell.BeginInvoke<PSObject, TOut>(null, output),
                powershell.EndInvoke);

        internal PowerShell AddInput(object? inputObject)
        {
            const string SetVariableCommand = "Set-Variable";
            const string DollarUnderbar = "_";

            if (inputObject is not null)
                powershell
                    .AddCommand(SetVariableCommand, useLocalScope: true)
                    .AddArgument(DollarUnderbar)
                    .AddArgument(inputObject);

            return powershell;
        }

        internal PowerShell AddScript(TaskSettings settings)
        {
            powershell.AddScript(settings.Script, useLocalScope: true);
            return powershell;
        }

        internal PowerShell AddUsingStatements(TaskSettings settings)
        {
            const string StopParsingOp = "--%";

            if (settings.UsingStatements.Count > 0)
                powershell.AddParameter(StopParsingOp, settings.UsingStatements);

            return powershell;
        }

        internal PowerShell WithStreams(PSOutputStreams outputStreams)
        {
            PSDataStreams streams = powershell.Streams;
            streams.Error = outputStreams.Error;
            streams.Debug = outputStreams.Debug;
            streams.Information = outputStreams.Information;
            streams.Progress = outputStreams.Progress;
            streams.Verbose = outputStreams.Verbose;
            streams.Warning = outputStreams.Warning;
            return powershell;
        }
    }

    extension(Task task)
    {
        internal ConfiguredTaskAwaitable NoContext() => task.ConfigureAwait(false);
    }

    extension<T>(Task<T> task)
    {
        internal ConfiguredTaskAwaitable<T> NoContext() => task.ConfigureAwait(false);
    }

    extension<TSource>(IEnumerable<TSource> source)
    {
        public IEnumerable<TSource> DistinctBy<TKey>(Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = [];
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                    yield return element;
            }
        }
    }

    extension(string x)
    {
        internal bool Matches(string y) =>
            x.Equals(y, StringComparison.OrdinalIgnoreCase)
            || WildcardPattern.Get(x, WildcardOptions.IgnoreCase).IsMatch(y);
    }
}
