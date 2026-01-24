using Microsoft.AspNetCore.Mvc;
using MediatR;
using BetsTrading.Application.Queries.FinancialAssets;
using BetsTrading.Application.DTOs;
using System.IO;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FinancialAssetsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationLogger _logger;

    public FinancialAssetsController(IMediator mediator, IApplicationLogger logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<BetsTrading.Application.DTOs.FinancialAssetDto>>> GetFinancialAssets(CancellationToken cancellationToken)
    {
        var query = new GetFinancialAssetsQuery();
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("ByGroup")]
    public async Task<ActionResult<IEnumerable<BetsTrading.Application.DTOs.FinancialAssetDto>>> GetFinancialAssetsByGroup(
        [FromBody] GetFinancialAssetsByGroupQuery? query, 
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.Debug("[FINANCIAL] :: ByGroup :: Request received. Query is null: {isNull}, Group: {group}, Id: {id}, Currency: {currency}, CurrencyJson: {currencyJson}", 
                query == null, query?.Group ?? "null", query?.Id ?? "null", query?.Currency ?? "null", query?.CurrencyJson ?? "null");
            
            // Handle null query
            if (query == null)
            {
                query = new GetFinancialAssetsByGroupQuery();
            }
            
            // Use computed properties to get the actual values
            var group = query.GetGroup();
            var currency = query.GetCurrency();
            
            // Set the resolved values in the query for the handler (using Currency property, not CurrencyJson)
            query.Group = group;
            query.Currency = currency; // This sets the internal Currency property
            
            // Handle empty Group (empty body, malformed JSON, or model binding failed)
            if (string.IsNullOrEmpty(query.Group))
            {
                _logger.Debug("[FINANCIAL] :: ByGroup :: Query is null or Group is empty, attempting manual parsing");
                
                // Try to read raw body to provide better error message
                // Note: EnableBuffering must be called before the body is read for the first time
                // If model binding already read it, we need to check if we can rewind
                try
                {
                    // El middleware ya debería haber habilitado el buffering, pero verificamos por si acaso
                    if (!Request.Body.CanSeek)
                    {
                        _logger.Warning("[FINANCIAL] :: ByGroup :: Body cannot seek, enabling buffering now (should have been done by middleware)");
                        Request.EnableBuffering();
                    }
                    
                    Request.Body.Position = 0;
                    
                    using var reader = new StreamReader(Request.Body, System.Text.Encoding.UTF8);
                    var rawBody = await reader.ReadToEndAsync();
                    Request.Body.Position = 0;
                    
                    _logger.Debug("[FINANCIAL] :: ByGroup :: Raw body length: {length}, CanSeek: {canSeek}, Content: {body}", 
                        rawBody.Length,
                        Request.Body.CanSeek,
                        rawBody.Length > 200 ? rawBody.Substring(0, 200) + "..." : rawBody);
                    
                    if (string.IsNullOrWhiteSpace(rawBody))
                    {
                        _logger.Warning("[FINANCIAL] :: ByGroup :: Empty request body. CanSeek: {canSeek}, Position: {pos}", 
                            Request.Body.CanSeek, Request.Body.CanSeek ? Request.Body.Position : -1);
                        return BadRequest(new { Message = "Request body is required. Expected JSON: {\"id\": \"group-name\", \"currency\": \"EUR\"}" });
                    }
                    
                    // Try to parse manually to handle both formats
                    try
                    {
                        var jsonDoc = System.Text.Json.JsonDocument.Parse(rawBody);
                        query ??= new GetFinancialAssetsByGroupQuery();
                        
                        _logger.Debug("[FINANCIAL] :: ByGroup :: JSON parsed successfully. Root element properties: {props}", 
                            string.Join(", ", jsonDoc.RootElement.EnumerateObject().Select(p => p.Name)));
                        
                        // Try snake_case first (client format: {'id': group, 'currency': currency})
                        if (jsonDoc.RootElement.TryGetProperty("id", out var idElement))
                        {
                            query.Group = idElement.GetString() ?? string.Empty;
                            _logger.Debug("[FINANCIAL] :: ByGroup :: Found 'id' property: {id}", query.Group);
                        }
                        // Try PascalCase (Group)
                        else if (jsonDoc.RootElement.TryGetProperty("Group", out var groupElement))
                        {
                            query.Group = groupElement.GetString() ?? string.Empty;
                            _logger.Debug("[FINANCIAL] :: ByGroup :: Found 'Group' property: {group}", query.Group);
                        }
                        else
                        {
                            _logger.Warning("[FINANCIAL] :: ByGroup :: Neither 'id' nor 'Group' property found in JSON");
                        }
                        
                        // Currency is optional, default is "EUR"
                        if (jsonDoc.RootElement.TryGetProperty("currency", out var currencyElement2))
                        {
                            query.Currency = currencyElement2.GetString() ?? "EUR";
                            _logger.Debug("[FINANCIAL] :: ByGroup :: Found 'currency' property: {currency}", query.Currency);
                        }
                        else if (jsonDoc.RootElement.TryGetProperty("Currency", out var currencyElement))
                        {
                            query.Currency = currencyElement.GetString() ?? "EUR";
                            _logger.Debug("[FINANCIAL] :: ByGroup :: Found 'Currency' property: {currency}", query.Currency);
                        }
                        else
                        {
                            query.Currency = "EUR";
                            _logger.Debug("[FINANCIAL] :: ByGroup :: No currency property found, using default: EUR");
                        }
                    }
                    catch (Exception parseEx)
                    {
                        _logger.Warning("[FINANCIAL] :: ByGroup :: JSON parsing error: {error}", parseEx.Message);
                        return BadRequest(new { Message = "Invalid request format. Expected JSON: {\"id\": \"group-name\", \"currency\": \"EUR\"}" });
                    }
                    
                    if (string.IsNullOrEmpty(query.Group))
                    {
                        _logger.Warning("[FINANCIAL] :: ByGroup :: Group is still empty after parsing");
                        return BadRequest(new { Message = "Request body must contain 'id' or 'Group' property" });
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning("[FINANCIAL] :: ByGroup :: Error reading request body: {error}", ex.Message);
                    return BadRequest(new { Message = "Request body is required. Expected JSON: {\"id\": \"group-name\", \"currency\": \"EUR\"}" });
                }
            }

            _logger.Debug("[FINANCIAL] :: ByGroup :: Sending query to mediator. Group: {group}, Currency: {currency}", 
                query.Group, query.Currency);

            var result = await _mediator.Send(query, cancellationToken);
            
            _logger.Debug("[FINANCIAL] :: ByGroup :: Mediator returned {count} results", result?.Count() ?? 0);
            
            if (result == null || !result.Any())
            {
                _logger.Debug("[FINANCIAL] :: ByGroup :: No results found for Group: {group}, Currency: {currency}", 
                    query.Group, query.Currency);
                return NotFound();
            }

            // Materialize the result to avoid lazy evaluation issues during serialization
            var resultList = result.ToList();
            _logger.Debug("[FINANCIAL] :: ByGroup :: Materialized {count} assets to list", resultList.Count);
            
            try
            {
                var response = Ok(resultList);
                _logger.Debug("[FINANCIAL] :: ByGroup :: Response created successfully. Returning Ok result with {count} items", resultList.Count);
                return response;
            }
            catch (Exception serializationEx)
            {
                _logger.Warning("[FINANCIAL] :: ByGroup :: Error creating response: {error}", serializationEx.Message);
                throw;
            }
        }
        catch (System.Text.Json.JsonException jsonEx)
        {
            _logger.Warning("[FINANCIAL] :: ByGroup :: JSON exception: {error}", jsonEx.Message);
            return BadRequest(new { Message = "Invalid JSON format in request body", Error = "JSON parsing error" });
        }
        catch (Exception ex)
        {
            _logger.Warning("[FINANCIAL] :: ByGroup :: Exception: {error}", ex.Message);
            return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
    }

    [HttpPost("ByCountry")]
    public async Task<ActionResult<IEnumerable<BetsTrading.Application.DTOs.FinancialAssetDto>>> GetFinancialAssetsByCountry(
        [FromBody] GetFinancialAssetsByCountryQuery query, 
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    [HttpPost("FetchCandles")]
    public async Task<ActionResult<IEnumerable<CandleDto>>> FetchCandles(
        [FromBody] FetchCandlesQuery query, 
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);
        
        if (!result.Success)
        {
            return NotFound(new { Message = result.Message });
        }

        return Ok(result.Candles);
    }
}
