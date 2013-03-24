// NAnt - A .NET build tool
// Copyright (C) 2001-2002 Gerry Shaw
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//
// Gerry Shaw (gerry_shaw@yahoo.com)
// Mike Krueger (mike@icsharpcode.net)
// Gert Driesen (drieseng@users.sourceforge.net)
// Ian MacLean (ian_maclean@another.com)
// Giuseppe Greco (giuseppe.greco@agamura.com)

using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

using NAnt.Core;
using NAnt.Core.Attributes;
using NAnt.Core.Types;
using NAnt.Core.Util;

using NAnt.DotNet.Types;

namespace NAnt.DotNet.Tasks {
    /// <summary>
    /// Compiles C# programs.
    /// </summary>
    /// <remarks>
    /// <note>
    /// In order to have <see cref="CscTask" /> generate manifest resource names
    /// that match those generated by Microsoft Visual Studio.NET, the value of
    /// the <see cref="ResourceFileSet.Prefix" /> attribute of the &lt;<see cref="CompilerBase.ResourcesList" />&gt;
    /// element should match the "Default Namespace" of the C# project, and the 
    /// value of the <see cref="ResourceFileSet.DynamicPrefix" /> attribute 
    /// should be set to &quot;<see langword="true" />&quot;.
    /// </note>
    /// </remarks>
    /// <example>
    ///   <para>Compile a "HelloWorld" application, including embedded resources.</para>
    ///   <code>
    ///     <![CDATA[
    /// <csc target="exe" output="HelloWorld.exe" debug="true">
    ///     <nowarn>
    ///         <!-- do not report warnings for missing XML comments -->
    ///         <warning number="0519" />
    ///     </nowarn>
    ///     <sources>
    ///         <include name="**/*.cs" />
    ///     </sources>
    ///     <resources dynamicprefix="true" prefix="HelloWorld">
    ///         <include name="**/*.resx" />
    ///     </resources>
    ///     <references>
    ///         <include name="System.dll" />
    ///         <include name="System.Data.dll" />
    ///     </references>
    /// </csc>
    ///     ]]>
    ///   </code>
    /// </example>
    [TaskName("csc")]
    [ProgramLocation(LocationType.FrameworkDir)]
    public class CscTask : CompilerBase {
        #region Private Instance Fields

        private DebugOutput _debugOutput = DebugOutput.None;
        private FileInfo _docFile;
        private int _fileAlign;
        private bool _nostdlib;
        private bool _noconfig;
        private bool _checked;
        private bool _unsafe;
        private bool _optimize;
        private string _warningLevel;
        private string _codepage;
        private string _baseAddress;
        private string _platform;
        private string _langVersion;

        // framework configuration settings
        private double _mcsSdk = 0;
        private bool _supportsDocGeneration = true;
        private bool _supportsPlatform;
        private bool _supportsLangVersion;

        #endregion Private Instance Fields

        #region Private Static Fields

        private static Regex _classNameRegex = new Regex(@"^((?<comment>/\*.*?(\*/|$))|[\s\.\{]+|class\s+(?<class>\w+)|(?<keyword>\w+))*");
        private static Regex _namespaceRegex = new Regex(@"^((?<comment>/\*.*?(\*/|$))|[\s\.\{]+|namespace\s+(?<namespace>(\w+(\.\w+)*)+)|(?<keyword>\w+))*");

        #endregion Private Static Fields

        #region Public Instance Properties

        /// <summary>
        /// The preferred base address at which to load a DLL. The default base 
        /// address for a DLL is set by the .NET Framework common language 
        /// runtime.
        /// </summary>
        /// <value>
        /// The preferred base address at which to load a DLL.
        /// </value>
        /// <remarks>
        /// This address can be specified as a decimal, hexadecimal, or octal 
        /// number. 
        /// </remarks>
        [TaskAttribute("baseaddress")]
        public string BaseAddress {
            get { return _baseAddress; }
            set { _baseAddress = StringUtils.ConvertEmptyToNull(value); }
        }

        /// <summary>
        /// Specifies the type of debugging information generated by the 
        /// compiler. The default is <see cref="T:NAnt.DotNet.Types.DebugOutput.None" />.
        /// </summary>
        [TaskAttribute("debug")]
        public DebugOutput DebugOutput {
            get { return _debugOutput; }
            set { _debugOutput = value; }
        }

        /// <summary>
        /// No longer expose this to build authors. Use <see cref="DebugOutput" />
        /// instead.
        /// </summary>
        public override bool Debug {
            get { return DebugOutput != DebugOutput.None; }
            set { DebugOutput = DebugOutput.Enable; }
        }

        /// <summary>
        /// The name of the XML documentation file to generate.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Corresponds with the <c>/doc:</c> flag.
        /// </para>
        /// </remarks>
        [TaskAttribute("doc")]
        public FileInfo DocFile {
            get { return _docFile; }
            set { _docFile = value; }
        }

        /// <summary>
        /// Specifies the size of sections in the output file. Valid values are
        /// 512, 1024, 2048, 4096, and 8192.
        /// </summary>
        /// <value>
        /// The size of sections in the output file.
        /// </value>
        [TaskAttribute("filealign")]
        [Int32Validator(512, 8192)]
        public int FileAlign {
            get { return _fileAlign; }
            set { _fileAlign = value; }
        }

        /// <summary>
        /// Instructs the compiler not to import mscorlib.dll. The default is 
        /// <see langword="false" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Corresponds with the <c>/nostdlib[+|-]</c> flag.
        /// </para>
        /// </remarks>
        [FrameworkConfigurable("nostdlib")]
        [TaskAttribute("nostdlib")]
        [BooleanValidator()]
        public bool NoStdLib {
            get { return _nostdlib; }
            set { _nostdlib = value; }
        }

        /// <summary>
        /// Instructs the compiler not to use implicit references to assemblies.
        /// The default is <see langword="false" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Corresponds with the <c>/noconfig</c> flag.
        /// </para>
        /// </remarks>
        [FrameworkConfigurable("noconfig")]
        [TaskAttribute("noconfig")]
        [BooleanValidator()]
        public bool NoConfig {
            get { return _noconfig; }
            set { _noconfig = value; }
        }

        /// <summary>
        /// Specifies whether an integer arithmetic statement that is not in 
        /// the scope of the <c>checked</c> or <c>unchecked</c> keywords and 
        /// that results in a value outside the range of the data type should 
        /// cause a run-time exception. The default is <see langword="false" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Corresponds with the <c>/checked[+|-]</c> flag.
        /// </para>
        /// </remarks>
        [TaskAttribute("checked")]
        [BooleanValidator()]
        public bool Checked {
            get { return _checked; }
            set { _checked = value; }
        }

        /// <summary>
        /// Instructs the compiler to allow code that uses the <c>unsafe</c> 
        /// keyword. The default is <see langword="false" />.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Corresponds with the <c>/unsafe[+|-]</c> flag.
        /// </para>
        /// </remarks>
        [TaskAttribute("unsafe")]
        [BooleanValidator()]
        public bool Unsafe {
            get { return _unsafe; }
            set { _unsafe = value; }
        }

        /// <summary>
        /// Causes the compiler to only accept syntax that is included in a
        /// given specification.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Corresponds with the <c>/langversion</c> flag.
        /// </para>
        /// </remarks>
        [TaskAttribute("langversion")]
        public string LangVersion {
            get { return _langVersion; }
            set { _langVersion = StringUtils.ConvertEmptyToNull(value); }
        }

        /// <summary>
        /// Specifies whether the compiler should perform optimizations to the 
        /// make output files smaller, faster, and more effecient. The default 
        /// is <see langword="false" />.
        /// </summary>
        /// <value>
        /// <see langword="true" /> if the compiler should perform optimizations; 
        /// otherwise, <see langword="false" />.
        /// </value>
        /// <remarks>
        /// <para>
        /// Corresponds with the <c>/optimize[+|-]</c> flag.
        /// </para>
        /// </remarks>
        [TaskAttribute("optimize")]
        [BooleanValidator()]
        public bool Optimize {
            get { return _optimize; }
            set { _optimize = value; }
        }

        /// <summary>
        /// Specifies which platform version of common language runtime (CLR)
        /// can run the output file.
        /// </summary>
        /// <value>
        /// The platform version of common language runtime (CLR) that can run
        /// the output file.
        /// </value>
        /// <remarks>
        /// <para>
        /// Corresponds with the <c>/platform</c> flag.
        /// </para>
        /// </remarks>
        [TaskAttribute("platform")]
        public string Platform {
            get { return _platform; }
            set { _platform = StringUtils.ConvertEmptyToNull(value); }
        }

        /// <summary>
        /// Specifies the warning level for the compiler to display. Valid values 
        /// are <c>0</c>-<c>4</c>. The default is <c>4</c>.
        /// </summary>
        /// <value>
        /// The warning level for the compiler to display.
        /// </value>
        /// <remarks>
        /// <para>
        /// Corresponds with the <c>/warn</c> flag.
        /// </para>
        /// </remarks>
        [TaskAttribute("warninglevel")]
        [Int32Validator(0, 4)]
        public string WarningLevel {
            get { return _warningLevel; }
            set { _warningLevel = StringUtils.ConvertEmptyToNull(value); }
        }

        /// <summary>
        /// Specifies the code page to use for all source code files in the 
        /// compilation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Corresponds with the <c>/codepage</c> flag.
        /// </para>
        /// </remarks>
        [TaskAttribute("codepage")]
        public string Codepage {
            get { return _codepage; }
            set { _codepage = StringUtils.ConvertEmptyToNull(value); }
        }

        /// <summary>
        /// Gets or sets the mcs sdk version to apply to the new mcs compiler
        /// for Mono 3.0+
        /// </summary>
        [FrameworkConfigurable("mcssdk")]
        public double McsSdk 
        {
            get { return _mcsSdk; }
            set { _mcsSdk = value; }
        }

        /// <summary>
        /// Specifies whether the compiler for the active target framework
        /// supports generation of XML Documentation file. The default is 
        /// <see langword="true" />.
        /// </summary>
        [FrameworkConfigurable("supportsdocgeneration")]
        public bool SupportsDocGeneration {
            get { return _supportsDocGeneration; }
            set { _supportsDocGeneration = value; }
        }

        /// <summary>
        /// Specifies whether the compiler for the active target framework
        /// supports limiting the platform on which the compiled code can run.
        /// The default is <see langword="false" />.
        /// </summary>
        [FrameworkConfigurable("supportsplatform")]
        public bool SupportsPlatform {
            get { return _supportsPlatform; }
            set { _supportsPlatform = value; }
        }

        /// <summary>
        /// Specifies whether the compiler for the active target framework
        /// supports accepting only a specific language syntax.
        /// The default is <see langword="false" />.
        /// </summary>
        [FrameworkConfigurable("supportslangversion")]
        public bool SupportsLangVersion {
            get { return _supportsLangVersion; }
            set { _supportsLangVersion = value; }
        }

        #endregion Public Instance Properties

        #region Override implementation of CompilerBase

        /// <summary>
        /// Writes the compiler options to the specified <see cref="TextWriter" />.
        /// </summary>
        /// <param name="writer"><see cref="TextWriter" /> to which the compiler options should be written.</param>
        protected override void WriteOptions(TextWriter writer) {
            // causes the compiler to specify the full path of the file in which 
            // an error was found
            WriteOption(writer, "fullpaths");

            // the base address for the DLL
            if (BaseAddress != null) {
                WriteOption(writer, "baseaddress", BaseAddress);
            }

            // If mcs is the compiler and the specified McsSdk version is specified, append the new
            // -sdk: option to the argument list.
            if (PlatformHelper.IsMono) 
            {
                if (ExeName.Equals("mcs", StringComparison.InvariantCultureIgnoreCase) && _mcsSdk > 0) 
                {
                    WriteOption(writer, "sdk", _mcsSdk.ToString());
                }
            }

            // XML documentation
            if (DocFile != null) {
                if (SupportsDocGeneration) {
                    WriteOption(writer, "doc", DocFile.FullName);
                } else {
                    Log(Level.Warning, ResourceUtils.GetString("String_CompilerDoesNotSupportXmlDoc"),
                        Project.TargetFramework.Description);
                }
            }

            // langversion
            if (LangVersion != null) {
                if (SupportsLangVersion) {
                    WriteOption(writer, "langversion", LangVersion);
                } else {
                    Log(Level.Warning, ResourceUtils.GetString("String_CompilerDoesNotSupportLangVersion"),
                        Project.TargetFramework.Description);
                }
            }

            // platform
            if (Platform != null) {
                if (SupportsPlatform) {
                    WriteOption(writer, "platform", Platform);
                } else {
                    Log(Level.Warning, ResourceUtils.GetString("String_CompilerDoesNotSupportPlatform"),
                        Project.TargetFramework.Description);
                }
            }

            // win32res
            if (Win32Res != null) {
                WriteOption (writer, "win32res", Win32Res.FullName);
            }

            // handle debug builds.
            switch (DebugOutput) {
                case DebugOutput.None:
                    break;
                case DebugOutput.Enable:
                    WriteOption(writer, "debug");
                    WriteOption(writer, "define", "DEBUG");
                    WriteOption(writer, "define", "TRACE");
                    break;
                case DebugOutput.Full:
                    WriteOption(writer, "debug");
                    break;
                case DebugOutput.PdbOnly:
                    WriteOption(writer, "debug", "pdbonly");
                    break;
                default:
                    throw new BuildException(string.Format(CultureInfo.InvariantCulture,
                        ResourceUtils.GetString("NA2011"), DebugOutput), Location);
            }

            if (FileAlign > 0) {
                WriteOption(writer, "filealign", FileAlign.ToString(CultureInfo.InvariantCulture));
            }

            if (NoStdLib) {
                WriteOption(writer, "nostdlib");
            }

            if (Checked) {
                WriteOption(writer, "checked");
            }

            if (Unsafe) {
                WriteOption(writer, "unsafe");
            }

            if (Optimize) {
                WriteOption(writer, "optimize");
            }

            if (WarningLevel != null) {
                WriteOption(writer, "warn", WarningLevel);
            }

            if (Codepage != null) {
                WriteOption(writer, "codepage", Codepage);
            }
        
            if (NoConfig && !Arguments.Contains("/noconfig")) {
                Arguments.Add(new Argument("/noconfig"));
            }
        }

        /// <summary>
        /// Determines whether compilation is needed.
        /// </summary>
        protected override bool NeedsCompiling() {
            if (base.NeedsCompiling()) {
                return true;
            }

            if (DocFile != null && SupportsDocGeneration) {
                if (!DocFile.Exists) {
                    Log(Level.Verbose, ResourceUtils.GetString("String_DocFileDoesNotExist"),
                        DocFile.FullName);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the file extension required by the current compiler.
        /// </summary>
        /// <value>
        /// For the C# compiler, the file extension is always <c>cs</c>.
        /// </value>
        public override string Extension {
            get { return "cs"; }
        }

        /// <summary>
        /// Gets the class name regular expression for the language of the 
        /// current compiler.
        /// </summary>
        /// <value>
        /// Class name regular expression for the language of the current 
        /// compiler.
        /// </value>
        protected override Regex ClassNameRegex {
            get { return _classNameRegex; }
        }
        /// <summary>
        /// Gets the namespace regular expression for the language of the current compiler.
        /// </summary>
        /// <value>
        /// Namespace regular expression for the language of the current 
        /// compiler.
        /// </value>
        protected override Regex NamespaceRegex {
            get { return _namespaceRegex; }
        }
        
        #endregion Override implementation of CompilerBase
    }
}
