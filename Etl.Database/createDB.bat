@echo off
REM если sqlcmd не прописан в Path, искать его тут: C:\Program Files\Microsoft SQL Server\110\Tools\Binn\
sqlcmd -i Etl.Database.publish.sql