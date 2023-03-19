// Set up the .NET WebAssembly runtime
import { dotnet } from './dotnet.js'

// Get exported methods from the .NET assembly
const { getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

// Access JSExport methods using exports.<Namespace>.<Type>.<Method>
const nowRaw = exports.Time.Now;
const parseRaw = exports.Time.Parse;

const parse = function (value, type) {
    const parsed = parseRaw(value, type);
    return JSON.parse(parsed);
};

const queriesStorage = {
    /*
    * v1 storage format is array of
    * {
    *   "index":0,
    *   "value":{
    *     "type":"DateTime",
    *     "dateTime":"2023-03-18T12:32:21Z",
    *     "unixTime":"1679142741",
    *     "ticks":"638147395410000000"
    *   }
    * }
    *
    * v2 storage format is array of
    * {
    *   "type": "DateTime",
    *   "value": "2023-03-18T12:32:21Z"
    * }
    */
    key: "data",
    fromV1Storage: function (element) {
        const value = element.value;
        const input = getField(value, value.type);
        return parse(input, value.type);
    },
    read: function () {
        try {
            return (JSON.parse(
                window.localStorage.getItem(this.key)
            ) || [])
                .map(
                    (element, index) => ({
                        index: index,
                        origin: "local-storage",
                        value: "index" in element
                            ? this.fromV1Storage(element) // v1 format
                            : parse(element.value, element.type) // v2 format
                    })
                ).filter(x => !!x);
        } catch {
            return [];
        }
    },
    write: function (queries) {
        const data = JSON.stringify(
            queries.map(query => ({ type: query.value.type, value: getField(query.value, query.value.type) }))
        );
        try {
            window.localStorage.setItem(this.key, data);
        } catch {
            // suppress any errors
        }
    }
};

const getField = function (obj, type) {
    const fieldMap = {
        DateTime: "dateTime",
        UnixTime: "unixTime",
        UnixTimeSeconds: "unixTimeSeconds",
        UnixTimeMilliseconds: "unixTimeMilliseconds",
        UnixTimeMicroseconds: "unixTimeMicroseconds",
        Ticks: "ticks",
        TimeGuid: "timeGuid"
    };
    const name = fieldMap[type];
    return obj[name];
};



const readQueries = queriesStorage.read();

const hashParts = document.location.hash
    ? decodeURI(document.location.hash.slice(1)).split("@")
    : [];

const knownTypes = [
    "Guess",
    "DateTime",
    "UnixTime (seconds)",
    "UnixTime (milliseconds)",
    "UnixTime (microseconds)",
    "UnixTime (guess)",
    "Ticks",
    "TimeGuid"
];

const startInput = hashParts[0] || nowRaw();
const startType = hashParts[1] && knownTypes.find(type => type === hashParts[1]) || "Guess";

window.app = new Vue({
    el: '#app',
    data: {
        state: "Typing",
        index: readQueries.length,
        input: startInput,
        type: startType,
        columns: {
            dateTime: true,
            unixTimeSeconds: true,
            unixTimeMilliseconds: false,
            unixTimeMicroseconds: false,
            ticks: true,
            timeGuid: false
        },
        parsedQueries: readQueries,
        flushFailed: false
    },
    methods: {
        flush: function () {
            if (this.parsed) {
                this.parsedQueries.unshift(
                    { index: this.index, origin: "input", value: this.parsed }
                );
                this.index++;
            } else {
                this.state = "FlushFailed";
            }
        },
        remove: function (index) {
            this.parsedQueries.splice(index, 1);
        },
        flushFailureIndicated: function () {
            this.state = "Typing";
        },
        hasParsedQueryWithType: function (type) {
            return (this.parsed && this.parsed.type === type) ||
                !!this.parsedQueries.find(query => query.value.type === type);
        }
    },
    computed: {
        parsed: function() {
            return parse(this.input, this.csharpType);
        },
        inputClass: function () {
            if (this.input.trim() === '') {
                return "input-empty";
            } else if (this.parsed) {
                return "input-parsed";
            } else if (this.state === "FlushFailed") {
                return "input-not-parsed failed-flush";
            } else {
                return "input-not-parsed";
            }
        },
        shownColumns: function () {
            return {
                dateTime:             this.columns.dateTime             || this.csharpType === "DateTime"             || this.hasParsedQueryWithType("DateTime"),
                unixTimeSeconds:      this.columns.unixTimeSeconds      || this.csharpType === "UnixTimeSeconds"      || this.hasParsedQueryWithType("UnixTimeSeconds"),
                unixTimeMilliseconds: this.columns.unixTimeMilliseconds || this.csharpType === "UnixTimeMilliseconds" || this.hasParsedQueryWithType("UnixTimeMilliseconds"),
                unixTimeMicroseconds: this.columns.unixTimeMicroseconds || this.csharpType === "UnixTimeMicroseconds" || this.hasParsedQueryWithType("UnixTimeMicroseconds"),
                ticks:                this.columns.ticks                || this.csharpType === "Ticks"                || this.hasParsedQueryWithType("Ticks"),
                timeGuid:             this.columns.timeGuid             || this.csharpType === "TimeGuid"             || this.hasParsedQueryWithType("TimeGuid")
            };
        },
        csharpType: function () {
            const type = this.type.trim();
            if (type === "UnixTime (seconds)") {
                return "UnixTimeSeconds";
            } else if (type === "UnixTime (milliseconds)") {
                return "UnixTimeMilliseconds";
            } else if (type === "UnixTime (microseconds)") {
                return "UnixTimeMicroseconds";
            } else if (type === "UnixTime (guess)") {
                return "UnixTimeGuess";
            } else {
                return type;
            }
        },
        hash: function () {
            const type = this.type.trim();
            const input = this.input.trim();
            return type === "Guess"
                ? input
                : `${input}@${type}`;
        }
    },
    watch: {
        hash: function () {
            try {
                document.location.hash = this.hash;
            }
            catch {
                // suppress any errors
            }
            this.state = "Typing";
        },
        parsedQueries: function () {
            queriesStorage.write(this.parsedQueries);
        }
    }
});

document.getElementById("root").className = "loaded";
document.getElementsByClassName("input")[0].focus();