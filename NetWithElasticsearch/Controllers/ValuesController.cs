﻿using Elasticsearch.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetWithElasticsearch.Context;
using Newtonsoft.Json.Linq;

namespace NetWithElasticsearch.Controllers;

[Route("api/[controller]")]
[ApiController]
public sealed class ValuesController : ControllerBase
{
    AppDbContext context = new AppDbContext();

    [HttpGet("[action]")]
    public async Task<IActionResult> CreateData(CancellationToken cancellationToken)
    {
        IList<Travel> travels = new List<Travel>();
        var random = new Random();

        for (int i = 0; i < 10000; i++)
        {
            var title = new string(Enumerable
                .Repeat("abcçdefgğhıijklmnoöprsştuüvyz", 5)
                .Select(s => s[random.Next(s.Length)]).ToArray());

            var words = new List<string>();
            for (int j = 0; j < 500; j++)
            {
                words.Add(new string(Enumerable
                    .Repeat("abcçdefgğhıijklmnoöprsştuüvyz", 5)
                    .Select(s => s[random.Next(s.Length)]).ToArray()));
            }

            var description = string.Join(" ", words);
            var travel = new Travel()
            {
                Title = title,
                Description = description
            };

            travels.Add(travel);
        }

        await context.Set<Travel>().AddRangeAsync(travels, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return Ok();
    }

    [HttpGet("[action]/{value}")]
    public async Task<IActionResult> GetDataListWithEF(string value)
    {
        IList<Travel> travels = await context
            .Set<Travel>()
            .Where(p => p.Title.Contains(value))
            .AsNoTracking()
            .ToListAsync();

        return Ok(travels.Take(10));
    }

    [HttpGet("[action]")]
    public async Task<IActionResult> SyncToElastic()
    {
        var settings = new ConnectionConfiguration(new Uri("http://localhost:9200"));

        var client = new ElasticLowLevelClient(settings);

        List<Travel> travels = await context.Set<Travel>().ToListAsync();

        var tasks = new List<Task>();

        foreach (var travel in travels)
        {
            tasks.Add(client.IndexAsync<StringResponse>("travels", travel.Id.ToString(), PostData.Serializable(new
            {
                travel.Id,
                travel.Title,
                travel.Description,
            })));
        }

        await Task.WhenAll(tasks);

        return Ok();
    }

    [HttpGet("[action]/{value}")]
    public async Task<IActionResult> GetDataListWithElasticsearch(string value)
    {
        var settings = new ConnectionConfiguration(new Uri("http://localhost:9200"));

        var client = new ElasticLowLevelClient(settings);

        var response = await client.SearchAsync<StringResponse>("travels", PostData.Serializable(new
        {
            query = new
            {
                wildcard = new
                {
                    Description = new { value = $"*{value}" }
                }
            }
        }));

        var results = JObject.Parse(response.Body);

        var hits = results["hits"]["hits"].ToObject<List<JObject>>();

        List<Travel> travels = new();

        foreach (var hit in hits)
        {
            travels.Add(hit["_source"].ToObject<Travel>());
        }

        return Ok(travels.Take(10));
    }

}
