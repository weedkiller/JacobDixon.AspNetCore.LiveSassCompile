﻿using JacobDixon.AspNetCore.LiveSassCompile.Compilers;
using JacobDixon.AspNetCore.LiveSassCompile.Exceptions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace JacobDixon.AspNetCore.LiveSassCompile
{
    public class SassFileWatcher
    {
        private const string _compileFileExtension = ".css";
        private FileSystemWatcher _fileWatcher;
        private readonly SassFileWatcherOptions _options;
        private Dictionary<string, DateTime> _lastRead = new Dictionary<string, DateTime>();
        private readonly ICompiler _compiler;

        public SassFileWatcher(SassFileWatcherOptions options, ICompiler compiler)
        {
            _compiler = compiler;
            _options = options;

            if (string.IsNullOrEmpty(_options.SourcePath))
                throw new EmptyStringException("SourcePath option must not be empty or null");

            if (string.IsNullOrEmpty(_options.DestinationPath))
                throw new EmptyStringException("DestinationPath option must not be empty or null");

            if (_options.FileNameFilters.Count == 0)
                throw new EmptyArrayException("FileNameFilters must contain atleast one filter");
        }

        public void StartFileWatcher()
        {
            _fileWatcher = new FileSystemWatcher(_options.SourcePath);

            foreach (var filter in _options.FileNameFilters)
                _fileWatcher.Filters.Add(filter);

            _fileWatcher.EnableRaisingEvents = true;
            _fileWatcher.IncludeSubdirectories = true;

            _fileWatcher.NotifyFilter = NotifyFilters.LastWrite
                                   | NotifyFilters.FileName
                                   | NotifyFilters.DirectoryName;

            _fileWatcher.Changed += FileWatcher_Changed;
            _fileWatcher.Created += FileWatcher_Changed;
            _fileWatcher.Renamed += FileWatcher_Renamed;
            _fileWatcher.Deleted += FileWatcher_Deleted;

            if (_options.CompileOnStart)
            {
                _compiler.Compile(_options.SourcePath);
            }
        }

        public void StopFileWatcher()
        {
            _fileWatcher.Changed -= FileWatcher_Changed;
            _fileWatcher.Created -= FileWatcher_Changed;
            _fileWatcher.Renamed -= FileWatcher_Renamed;
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher?.Dispose();
            _fileWatcher = null;
        }

        private void FileChanged(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            var lastWriteTime = File.GetLastWriteTime(filePath);

            if (!_lastRead.ContainsKey(filePath) || _lastRead[filePath] < lastWriteTime)
            {
                _compiler.Compile(filePath);

                _lastRead[filePath] = DateTime.Now;
            }
        }

        private void DeleteCompiledFile(string filePath)
        {
            if (filePath.IsNullOrEmpty())
                return;

            var fileName = Path.GetFileName(filePath);
            if (_compiler.IsExcluded(fileName))
                _compiler.Compile(_options.SourcePath);

            var cssFilePath = Path.ChangeExtension(filePath, _compileFileExtension);
            var relativePath = Path.GetRelativePath(_options.SourcePath, cssFilePath);
            var destinationPath = Path.Combine(_options.DestinationPath, relativePath);

            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }
        }

        private void FileWatcher_Renamed(object sender, RenamedEventArgs e)
        {
            DeleteCompiledFile(e.OldFullPath);
            FileChanged(e.FullPath);
        }

        private void FileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            FileChanged(e.FullPath);
        }

        private void FileWatcher_Deleted(object sender, FileSystemEventArgs e)
        {
            DeleteCompiledFile(e.FullPath);
        }
    }
}
