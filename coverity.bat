cov-build --dir cov-int "C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\MSBuild\15.0\Bin\amd64\MSBuild.exe" SpellWork.sln /t:Rebuild
tar czvf spellwork.tgz cov-int
REM upload spellwork.tgz to https://scan.coverity.com/projects/12196/builds/new?tab=upload
