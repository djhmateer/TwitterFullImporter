
select * from [dbo].[MiniProfilers] order by started desc


select * from [dbo].[MiniProfilerTimings]
where MiniProfilerId = '42CFEABA-877C-4EC5-83FC-F50FB249C14C'


--select * from [dbo].[MiniProfilerClientTimings]

select * from MiniProfilerTimings
WHERE IsRoot = 0
order by DurationMilliseconds

-- find largest
-- 6 console apps - 697 (after reboot)
-- Redis 33s
-- MSSQL 14s

-- 4 console apps
-- total 803s
-- Redis 28
-- MSSQL6.8S
  
select Name, max(DurationMilliseconds)
from MiniProfilerTimings
group by Name
order by max(DurationMilliseconds) desc

truncate table [dbo].[MiniProfilerTimings]
truncate table [dbo].[MiniProfilers]