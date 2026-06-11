<Project Sdk="Microsoft.NET.Sdk">

	<PropertyGroup>
		<OutputType>WinExe</OutputType>
		<TargetFramework>net8.0-windows</TargetFramework>
		<RootNamespace>Explorador_de_Archivo</RootNamespace>
		<Nullable>enable</Nullable>
		<UseWindowsForms>true</UseWindowsForms>
		<ImplicitUsings>enable</ImplicitUsings>
		<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
	</PropertyGroup>

	<ItemGroup>
		<Compile Update="Properties\Resources.Designer.cs">
			<DesignTime>True</DesignTime>
			<AutoGen>True</AutoGen>
			<DependentUpon>Resources.resx</DependentUpon>
		</Compile>
	</ItemGroup>

	<ItemGroup>
		<EmbeddedResource Update="Properties\Resources.resx">
			<Generator>ResXFileCodeGenerator</Generator>
			<LastGenOutput>Resources.Designer.cs</LastGenOutput>
		</EmbeddedResource>
	</ItemGroup>

	<ItemGroup>
		<PackageReference Include="AForge" Version="2.2.5" />
		<PackageReference Include="AForge.Controls" Version="2.2.5" />
		<PackageReference Include="AForge.Video" Version="2.2.5" />
		<PackageReference Include="AForge.Video.DirectShow" Version="2.2.5" />
		<PackageReference Include="AugusteVN.MailKit.SmtpMailer" Version="1.1.2" />
		<PackageReference Include="CsvHelper" Version="33.1.0" />
		<PackageReference Include="FileExplorer" Version="3.0.20" />
		<PackageReference Include="LibVLCSharp" Version="3.9.7.1" />
		<PackageReference Include="LibVLCSharp.WinForms" Version="3.9.7.1" />
		<PackageReference Include="LiveCharts.WinForms" Version="0.9.7.1" />
		<PackageReference Include="LiveCharts.Wpf" Version="0.9.7" />
		<PackageReference Include="MACTrackBarLib.dll" Version="1.0.2" />
		<PackageReference Include="MailKit" Version="4.17.0" />
		<PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.8" />
		<PackageReference Include="NAudio" Version="2.3.0" />
		<PackageReference Include="ScottPlot.WinForms" Version="5.1.58" />

		<PackageReference Include="TagLib.Audio" Version="1.1.0" />
		<PackageReference Include="TagLib.Portable" Version="1.3.1" />
		<PackageReference Include="TagLibSharp" Version="2.3.0" />
		<PackageReference Include="VideoLAN.LibVLC.Windows" Version="3.0.23.1" />
	</ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.1" />
    <PackageReference Include="Microsoft.Web.WebView2" Version="1.0.2592.51" />
  </ItemGroup>

</Project>