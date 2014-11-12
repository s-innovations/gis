using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SInnovations.Gis.OgrHelpers
{
    public class AsyncProcess : AsyncProcess<int>
    {
       public AsyncProcess(string processName)
           :base(processName,done)
       {

       }
       private static int done(Process p,string txt, string err)
       {
           if (p.ExitCode != 0)
               throw new Exception(err);
           return p.ExitCode;
       }
    }
    public interface IProcessReader
    {

    }
    public interface ILineReader :IProcessReader
    {
        Task<bool> ReadLineAsync(string str);
    }
    public interface ICharReader : IProcessReader
    {
        Task<bool> ReadCharAsync(string str);
    }
    public interface IDoneReader : IProcessReader
    {
        Task<bool> ReadResult(string str);
    }
    public interface IEnvironmentVariablesProvider
    {
        string GetEnvironmentVariable(string key);
        IEnumerable<string> EnvrionmentsVariables { get; }
    }
    public class DefaultEnvironmentVariableProvider :  IEnvironmentVariablesProvider
    {
        private IReadOnlyDictionary<string, string> _values;
      
        public DefaultEnvironmentVariableProvider(IReadOnlyDictionary<string, string> readOnlyValues)
        {
            _values = readOnlyValues;
        }
        public string GetEnvironmentVariable(string key)
        {
            if (!_values.ContainsKey(key))
                return null;
            return _values[key];
        }

        public IEnumerable<string> EnvrionmentsVariables { get { return _values.Keys; } }
    }
    public class AsyncProcess<T>
    {

        #region Extensions
        public IEnvironmentVariablesProvider EnvironmentVariables { get; set; }
        public String WorkingFolder { get; set; }
        #endregion

        private StringBuilder _output;
        private StringBuilder _error;
        private string _processName;
   
        private Func<Process,string,string, T> _resultFunc;

        public AsyncProcess(string processName, Func<Process,string,string,T> resultFunc)
        {
            _processName = processName;
            _output = new StringBuilder();
            _error = new StringBuilder();
            _resultFunc = resultFunc;
        }

        public Task<T> RunAsync(string arguments)
        {
            TaskCompletionSource<T> _source = new TaskCompletionSource<T>();
            Task.Factory.StartNew(() =>
            {
                System.Diagnostics.Process process = new System.Diagnostics.Process();
                System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = string.Format("/C {0} {1}", _processName, arguments);
                startInfo.RedirectStandardOutput = true;
                startInfo.RedirectStandardError = true;
                startInfo.UseShellExecute = false;
                process.StartInfo = startInfo;
                process.ErrorDataReceived += ErrorHandler;
                if (WorkingFolder!=null)
                {
                    startInfo.WorkingDirectory = WorkingFolder;
                }

                if (EnvironmentVariables!=null)
                {
                   // foreach(Match match in Regex.Matches(startInfo.Arguments, "%(.+)%"))
                    foreach(var key in EnvironmentVariables.EnvrionmentsVariables)
                    {
                        var value = EnvironmentVariables.GetEnvironmentVariable(key);
                        if (value != null)
                        {
                         
                                startInfo.EnvironmentVariables[key] = value;
                            
                        }
                    }
                }

                process.Start();

                //read error output in async way
                process.BeginErrorReadLine();


                Task.Run(() =>
                {
                    while (!process.HasExited)
                    {
                        process.StandardOutput.BaseStream.Flush();
                        _output.Append((char)process.StandardOutput.Read());
                        CheckOutput();
                    }
                });

                process.WaitForExit();
                _output.Append(process.StandardOutput.ReadToEnd());

                try
                {
                    _source.SetResult(_resultFunc(process, _output.ToString(), _error.ToString()));
                }catch(Exception ex)
                {
                    _source.SetException(ex);
                }

            },TaskCreationOptions.LongRunning);

            return _source.Task;
        }

        private void ErrorHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            _error.AppendLine(outLine.Data);
        }

        private void CheckOutput()
        {
            
        }
    }
}
