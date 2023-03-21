// Set up the .NET WebAssembly runtime
import { dotnet } from './dotnet.js'

// Get exported methods from the .NET assembly
const { getAssemblyExports, getConfig } = await dotnet
    .withDiagnosticTracing(false)
    .create();

const config = getConfig();
const exports = await getAssemblyExports(config.mainAssemblyName);

// Access JSExport methods using exports.<Namespace>.<Type>.<Method>
const now = exports.Time.Now;
const parse = exports.Time.Parse;

const key = "data";
const read = function () {
    try {
        return JSON.parse(
            window.localStorage.getItem(key)
        ) || [];
    } catch {
        return [];
    }
}

const store = function (parsedQueries) {
    const data = JSON.stringify(
        parsedQueries.map(item => ({ index: item.index, value: item.value }))
    );
    try {
        window.localStorage.setItem(key, data);
    } catch {
        // suppress any errors
    }
}

const storedEntries = read();
let index = 0;
for (let i = 0; i < storedEntries.length; i++) {
    index = Math.max(index, storedEntries[i].index + 1);
}

window.app = new Vue({
    el: '#app',
    data: {
        state: "Typing",
        index: index,
        input: document.location.hash ? decodeURI(document.location.hash.slice(1)) : now(),
        type: "Guess",
        parsedQueries: storedEntries.map(item => ({ index: item.index, origin: "local-storage", value: item.value })),
        flushFailed: false
    },
    methods: {
        flush: function () {
            if (this.parsed) {
                this.parsedQueries.unshift(
                    { index: this.index, origin: "input", value: this.parsed }
                );
                this.index++
            } else {
                this.state = "FlushFailed";
            }
        },
        remove: function (index) {
            this.parsedQueries.splice(index, 1);
        },
        flushFailureIndicated: function () {
            this.state = "Typing";
        }
    },
    computed: {
        parsed: function() {
            const value = parse(this.input, this.type);
            return JSON.parse(value);
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
        }
    },
    watch: {
        input: function () {
            document.location.hash = this.input;
            this.state = "Typing";
        },
        parsedQueries: function () {
            store(this.parsedQueries);
        }
    }
})

document.getElementById("root").className = "loaded"
document.getElementsByClassName("input")[0].focus();