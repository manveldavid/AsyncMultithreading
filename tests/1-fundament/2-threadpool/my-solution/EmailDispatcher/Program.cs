var emailDispatcher = new EmailDispatcher.EmailDispatcher();

emailDispatcher.ShowThreadPoolStats("before");
emailDispatcher.SendAllEmails(200);
emailDispatcher.SimulateStarvation(17);
emailDispatcher.ShowHillClimbingEffect(50);
emailDispatcher.CompareQueueUserWorkItemVsUnsafe(10000);