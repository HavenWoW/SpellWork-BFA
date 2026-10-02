cov-build --dir cov-int dotnet build SpellWork.sln -c Release --no-incremental --nologo
tar czvf spellwork.tgz cov-int
REM upload spellwork.tgz to https://scan.coverity.com/projects/12196/builds/new?tab=upload
