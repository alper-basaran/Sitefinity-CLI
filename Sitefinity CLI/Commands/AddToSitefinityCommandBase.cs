﻿using McMaster.Extensions.CommandLineUtils;
using Sitefinity_CLI.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sitefinity_CLI.Commands
{
    internal abstract class AddToSitefinityCommandBase : CommandBase
    {
        protected IEnumerable<FileModel> FileModels { get; set; }

        protected abstract string FolderPath { get; }

        protected abstract string CreatedMessage { get; }

        protected abstract string TemplatesFolder { get; }

        [Option(Constants.TemplateNameOptionTemplate, Constants.TemplateNameOptionDescription + Constants.DefaultSourceTemplateName, CommandOptionType.SingleValue)]
        [DefaultValue(Constants.DefaultSourceTemplateName)]
        public override string TemplateName { get; set; } = Constants.DefaultSourceTemplateName;

        protected string CamelCaseName
        {
            get
            {
                return this.ToPascalCase(this.Name);
            }
        }

        public override int OnExecute(CommandLineApplication config)
        {
            if (base.OnExecute(config) == 1)
            {
                return 1;
            }

            var folderPath = Path.Combine(this.ProjectRootPath, this.FolderPath);

            if (this.IsSitefinityProject)
            {
                Directory.CreateDirectory(folderPath);
            }
            else
            {
                if (!Directory.Exists(folderPath))
                {
                    Utils.WriteLine(string.Format(Constants.DirectoryNotFoundMessage, folderPath), ConsoleColor.Red);
                    return 1;
                }
            }

            var templatePath = Path.Combine(this.CurrentPath, Constants.TemplatesFolderName, this.Version, Constants.CustomWidgetTemplatesFolderName, this.TemplateName);

            if (!Directory.Exists(templatePath))
            {
                Utils.WriteLine(string.Format(Constants.TemplateNotFoundMessage, config.FullName, templatePath), ConsoleColor.Red);
                return 1;
            }

            this.FileModels = this.GetFileModels();

            if (this.AddToSitefinity(config) == 1)
            {
                return 1;
            }

            Utils.WriteLine(string.Format(this.CreatedMessage, this.Name), ConsoleColor.Green);
            if (this.filesAddedToCsProjResult == null || !this.filesAddedToCsProjResult.Success)
            {
                if (this.filesAddedToCsProjResult != null && !string.IsNullOrEmpty(this.filesAddedToCsProjResult.Message))
                {
                    Utils.WriteLine(this.filesAddedToCsProjResult.Message, ConsoleColor.Yellow);
                }

                Utils.WriteLine(Constants.AddFilesToProjectMessage, ConsoleColor.Yellow);
            }
            else
            {
                Utils.WriteLine(Constants.FilesAddedToProjectMessage, ConsoleColor.Green);
            }

            return 0;
        }

        protected override int CreateFileFromTemplate(string filePath, string templatePath, string resourceFullName, object data)
        {
            if (base.CreateFileFromTemplate(filePath, templatePath, resourceFullName, data) == 1)
            {
                throw new Exception(string.Format("An error occured while creating an item from template. Path: {0}", filePath));
            }

            this.createdFiles.Add(filePath);
            return 0;
        }

        protected int AddToSitefinity(CommandLineApplication config)
        {
            this.createdFiles = new List<string>();

            try
            {
                foreach (var fileModel in this.FileModels)
                {
                    var folderPath = Path.GetDirectoryName(fileModel.FilePath);

                    var data = this.GetTemplateData(Path.GetDirectoryName(fileModel.TemplatePath));
                    data["toolName"] = Constants.CLIName;
                    data["version"] = this.AssemblyVersion;
                    data["name"] = this.Name;

                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }

                    this.CreateFileFromTemplate(fileModel.FilePath, fileModel.TemplatePath, config.FullName, data);
                }

                this.filesAddedToCsProjResult = this.AddFilesToCsProj();
            }
            catch (Exception)
            {
                this.DeleteFiles();
                this.RemoveFilesFromCsproj();
                return 1;
            }


            return 0;
        }

        protected virtual ICollection<FileModel> GetFileModels()
        {
            return new List<FileModel>();
        }

        protected void DeleteFiles()
        {
            foreach (var filePath in this.createdFiles)
            {
                File.Delete(filePath);
            }
        }

        protected FileModifierResult AddFilesToCsProj()
        {
            string csprojFilePath = GetCsprojFilePath();
            FileModifierResult result = CsProjModifier.AddFiles(csprojFilePath, this.createdFiles);

            return result;
        }

        protected string GetCsprojFilePath()
        {
            string path = Directory.GetFiles(this.ProjectRootPath, $"*{Constants.CsprojFileExtension}").FirstOrDefault();

            return path;
        }

        protected void RemoveFilesFromCsproj()
        {
            string csProjFilePath = GetCsprojFilePath();
            CsProjModifier.RemoveFiles(csProjFilePath, this.createdFiles);
        }

        private string ToPascalCase(string s)
        {
            s = Regex.Replace(s, @"[^\-\!\$\(\)\=\@\d_\']+", " ", RegexOptions.IgnoreCase);

            var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.Substring(0, 1).ToUpper() + w.Substring(1));

            return string.Concat(words);
        }

        protected List<string> createdFiles;

        protected FileModifierResult filesAddedToCsProjResult;
    }
}
