// Copyright (c) .NET Foundation and contributors. All rights reserved. Licensed under the Microsoft Reciprocal License. See LICENSE.TXT file in the project root for full license information.

namespace WixToolset.Core.WindowsInstaller.Bind
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using WixToolset.Core.Bind;
    using WixToolset.Data;
    using WixToolset.Data.Tuples;

    internal class GetFileFacadesCommand
    {
        public GetFileFacadesCommand(IntermediateSection section)
        {
            this.Section = section;
        }

        private IntermediateSection Section { get; }

        public List<FileFacade> FileFacades { get; private set; }

        public void Execute()
        {
            var facades = new List<FileFacade>();

            var wixFiles = this.Section.Tuples.OfType<WixFileTuple>().ToDictionary(t => t.File_);
            var deltaPatchFiles = this.Section.Tuples.OfType<WixDeltaPatchFileTuple>().ToDictionary(t => t.File_);

            foreach (var file in this.Section.Tuples.OfType<FileTuple>())
            {
                var wixFile = wixFiles[file.File];

                deltaPatchFiles.TryGetValue(file.File, out var deltaPatchFile);

                facades.Add(new FileFacade(file, wixFile, deltaPatchFile));
            }

            this.ResolveDeltaPatchSymbolPaths(deltaPatchFiles, facades);

            this.FileFacades = facades;
        }

        /// <summary>
        /// Merge data from the WixPatchSymbolPaths rows into the WixDeltaPatchFile rows.
        /// </summary>
        public void ResolveDeltaPatchSymbolPaths(Dictionary<string, WixDeltaPatchFileTuple> deltaPatchFiles, IEnumerable<FileFacade> facades)
        {
            ILookup<string, FileFacade> filesByComponent = null;
            ILookup<string, FileFacade> filesByDirectory = null;
            ILookup<string, FileFacade> filesByDiskId = null;

            foreach (var row in this.Section.Tuples.OfType<WixDeltaPatchSymbolPathsTuple>().OrderBy(r => r.Type))
            {
                switch (row.Type)
                {
                    case SymbolPathType.File:
                        this.MergeSymbolPaths(row, deltaPatchFiles[row.Id]);
                        break;

                    case SymbolPathType.Component:
                        if (null == filesByComponent)
                        {
                            filesByComponent = facades.ToLookup(f => f.File.Component_);
                        }

                        foreach (var facade in filesByComponent[row.Id])
                        {
                            this.MergeSymbolPaths(row, deltaPatchFiles[facade.File.File]);
                        }
                        break;

                    case SymbolPathType.Directory:
                        if (null == filesByDirectory)
                        {
                            filesByDirectory = facades.ToLookup(f => f.WixFile.Directory_);
                        }

                        foreach (var facade in filesByDirectory[row.Id])
                        {
                            this.MergeSymbolPaths(row, deltaPatchFiles[facade.File.File]);
                        }
                        break;

                    case SymbolPathType.Media:
                        if (null == filesByDiskId)
                        {
                            filesByDiskId = facades.ToLookup(f => f.WixFile.DiskId.ToString(CultureInfo.InvariantCulture));
                        }

                        foreach (var facade in filesByDiskId[row.Id])
                        {
                            this.MergeSymbolPaths(row, deltaPatchFiles[facade.File.File]);
                        }
                        break;

                    case SymbolPathType.Product:
                        foreach (var fileRow in deltaPatchFiles.Values)
                        {
                            this.MergeSymbolPaths(row, fileRow);
                        }
                        break;

                    default:
                        // error
                        break;
                }
            }
        }

        /// <summary>
        /// Merge data from a row in the WixPatchSymbolsPaths table into an associated WixDeltaPatchFile row.
        /// </summary>
        /// <param name="row">Row from the WixPatchSymbolsPaths table.</param>
        /// <param name="file">FileRow into which to set symbol information.</param>
        /// <comment>This includes PreviousData as well.</comment>
        private void MergeSymbolPaths(WixDeltaPatchSymbolPathsTuple row, WixDeltaPatchFileTuple file)
        {
            if (file.SymbolPaths is null)
            {
                file.SymbolPaths = row.SymbolPaths;
            }
            else
            {
                file.SymbolPaths = String.Concat(file.SymbolPaths, ";", row.SymbolPaths);
            }

#if REVISIT_FOR_PATCHING
            Field field = row.Fields[2];
            if (null != field.PreviousData)
            {
                if (null == file.PreviousSymbols)
                {
                    file.PreviousSymbols = field.PreviousData;
                }
                else
                {
                    file.PreviousSymbols = String.Concat(file.PreviousSymbols, ";", field.PreviousData);
                }
            }
#endif
        }
    }
}
