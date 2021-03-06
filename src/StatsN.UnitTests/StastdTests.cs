﻿using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace StatsN.UnitTests
{
    public class StastdTests
    {
        public StatsdOptions options;
        public StastdTests()
        {
            var opt = new StatsdOptions
            {
                OnExceptionGenerated = (exception) => { throw exception; }
            };
            options = opt;
        }
        [Fact]
        public void BuildMetricNoPrefixTest()
        {
            var statsd = Statsd.New<NullChannel>(options);
            var output = statsd.BuildMetric("awesomeMetric.yo", "1", "c");
            Assert.Equal("awesomeMetric.yo:1|c", output);
        }
        [Theory]
        [InlineData("awesome.yo", "1", "c", "awesome.yo:1|c", "")]
        [InlineData("awesome.yo", "4", "s", "appname.awesome.yo:4|s", "appname")]
        public void TestManyMetrics(string metric, string value, string type, string assertionValue, string prefix = "")
        {
            var statsd = Statsd.New<NullChannel>(options);
            var output = statsd.BuildMetric(metric, value, type, prefix);
            Assert.Equal(assertionValue, output);
        }
        [Fact]
        public void BuildMetricPrefixTest()
        {
            var statsd = Statsd.New<NullChannel>(a => { a.HostOrIp = "localhost"; a.OnExceptionGenerated = (b) => { throw b; }; });
            var output = statsd.BuildMetric("awesomeMetric.yo", "1", "c", "myPrefix");
            Assert.Equal("myPrefix.awesomeMetric.yo:1|c", output);
        }
        [Fact]
        public void EscapeDots()
        {
            var options = new StatsdOptions() { Prefix = "awesome." };
            Assert.Equal("awesome", options.Prefix);
        }
        [Fact]
        public void ConstructWithoutProvider()
        {
            //check to make sure the constructor doesn't blow
            var t = new Statsd(new StatsdOptions()
            {
                OnExceptionGenerated = (exception) => { throw exception; }
            });
            t.CountAsync("awesome").GetAwaiter().GetResult();
            t.Dispose();
        }
        [Fact]
        public void CorrectMetricTypesPassed()
        {
            var statsd = Statsd.New<NullChannel>(a => { a.HostOrIp = "localhost"; a.OnExceptionGenerated = (b) => { throw b; }; });
            var output = statsd.BuildMetric("awesomeMetric.yo", "1", "c", "myPrefix");
            Assert.Equal("myPrefix.awesomeMetric.yo:1|c", output);
        }
        [Fact]
        public void BadMetricNamePassed()
        {
            var statsd = Statsd.New<NullChannel>(a => { a.HostOrIp = "localhost"; a.OnExceptionGenerated = (b) => { throw b; }; });
            var output = statsd.BuildMetric("", "1", "c", "myPrefix");
            Assert.True(string.IsNullOrEmpty(output));
        }
        [Fact]
        public void BadMetricValuePassed()
        {
            var statsd = Statsd.New<NullChannel>(a => { a.HostOrIp = "localhost"; a.OnExceptionGenerated = (b) => { throw b; }; });
            var output = statsd.BuildMetric("yodawg", "", "c", "myPrefix");
            Assert.True(string.IsNullOrEmpty(output));
        }
        [Fact]
        public void BadMetricTypePassed()
        {
            var statsd = Statsd.New<NullChannel>(a => { a.HostOrIp = "localhost"; a.OnExceptionGenerated = (b) => { throw b; }; });
            var output = statsd.BuildMetric("yodawg", "1", "", "myPrefix");
            Assert.True(string.IsNullOrEmpty(output));
        }
        [Fact]
        public void BuildMetricTiming()
        {
            var statsd = Statsd.New<NullChannel>(a => { a.HostOrIp = "localhost"; a.OnExceptionGenerated = (b) => { throw b; }; });
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            for (int i = 0; i < 100000; i++)
            {
                statsd.BuildMetric("awesomeMetric.yo", "1", "c", "myPrefix");
            }
            stopwatch.Stop();
            //we should be able to compile metrics FAST
            Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 3000);
        }
        [Fact]
        public void ConfirmMetricsBuffered()
        {
            var mockedMetric = new Mock<NullChannel>();
            mockedMetric.Setup(a => a.IsConnected).Returns(true);
            mockedMetric.Setup(a => a.SendAsync(It.Is<byte[]>(param => param.Length > 200 && param.Length < 512))).Verifiable();
            using (var statsd = new Statsd(new StatsdOptions() { BufferMetrics = true }, mockedMetric.Object))
            {
                for (int i = 0; i < 100000; i++)
                {
                    statsd.CountAsync("fun");
                }
                while (mockedMetric.Object.worker.IsBusy) { }
                mockedMetric.Verify();
            }
        }
        [Fact]
        public void ConfirmMetricsNotBuffered()
        {
            var mockedMetric = new Mock<NullChannel>();
            mockedMetric.Setup(a => a.IsConnected).Returns(true);
            mockedMetric.Setup(a => a.SendAsync(It.Is<byte[]>(param => param.Length < 50))).Verifiable();
            using (var statsd = new Statsd(new StatsdOptions() { }, mockedMetric.Object))
            {
                for (int i = 0; i < 100000; i++)
                {
                    statsd.CountAsync("fun");
                }
                while (mockedMetric.Object.worker.IsBusy) { }
                mockedMetric.Verify();
            }
        }
        [Fact]
        public void ConfirmUdpSendBuffTime()
        {
            var client = new Udp();
            var options = new StatsdOptions() { BufferMetrics = true, OnExceptionGenerated = (exception)=> { throw exception; } };
            using (var statsd = new Statsd(options, client))
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                for (int i = 0; i < 100000; i++)
                {
                    statsd.CountAsync("fun");
                }
                while (client.worker.IsBusy) { }
                stopwatch.Stop();
                //we shouldn't cost more than 1 milisecond a metric buffered or not
                Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 100000);
            }
        }
        [Fact]
        public void ConfirmUdpSendNoBufferedTime()
        {
            var client = new Udp();
            using (var statsd = new Statsd(new StatsdOptions() { BufferMetrics = false, OnExceptionGenerated = (exception) => { throw exception; } }, client))
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                for (int i = 0; i < 100000; i++)
                {
                    statsd.CountAsync("fun");
                }
                while (client.worker.IsBusy) { }
                stopwatch.Stop();
                //we shouldn't cost more than 1 milisecond a metric buffered or not
                Assert.InRange(stopwatch.ElapsedMilliseconds, 0, 100000);
            }
        }
    }
}
